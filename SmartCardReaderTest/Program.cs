using System;
using System.Collections.Generic;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;
using PCSC.Monitoring;

namespace SmartCardReaderTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                using (var context = ContextFactory.Instance.Establish(SCardScope.System))
                {
                    var readerNames = context.GetReaders();

                    if (readerNames.Length == 0)
                    {
                        Console.WriteLine("No smart card readers found.");
                        return;
                    }

                    Console.WriteLine($"Found {readerNames.Length} reader(s):");
                    foreach (var readerName in readerNames)
                    {
                        Console.WriteLine($"- {readerName}");
                    }

                    MonitorReaders(readerNames);
                }
            }
            catch (PCSCException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        static void MonitorReaders(string[] readerNames)
        {
            using (var monitor = MonitorFactory.Instance.Create(SCardScope.System))
            {
                var readerStates = new Dictionary<string, SCRState>();

                monitor.StatusChanged += (sender, e) =>
                {
                    if (!readerStates.TryGetValue(e.ReaderName, out var previousState))
                    {
                        previousState = SCRState.Unaware;
                    }

                    if (e.NewState.HasFlag(SCRState.Present) && !previousState.HasFlag(SCRState.Present))
                    {
                        Console.WriteLine($"Card inserted into {e.ReaderName}");
                        DetermineCardType(e.ReaderName);
                    }
                    else if (!e.NewState.HasFlag(SCRState.Present) && previousState.HasFlag(SCRState.Present))
                    {
                        Console.WriteLine($"Card removed from {e.ReaderName}");
                    }

                    readerStates[e.ReaderName] = e.NewState;
                };

                monitor.Start(readerNames);

                Console.WriteLine("Monitoring for card events. Press Enter to exit.");
                Console.ReadLine();

                monitor.Cancel();
            }
        }

        static void DetermineCardType(string readerName)
        {
            try
            {
                using (var context = ContextFactory.Instance.Establish(SCardScope.System))
                using (var reader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
                {
                    var atr = reader.GetAttrib(SCardAttribute.AtrString);
                    Console.WriteLine($"Card ATR: {BitConverter.ToString(atr)}");

                    // Enhanced ATR interpretation
                    if (atr[0] == 0x3B)
                    {
                        if (atr[1] == 0x6E)
                        {
                            Console.WriteLine("This might be a JavaCard or GP card");
                        }
                        else if (atr[1] == 0x67)
                        {
                            Console.WriteLine("This might be a MIFARE card");
                        }
                        else if (atr[1] == 0xF8 && atr.Length >= 10 && atr[8] == 0xFE)
                        {
                            Console.WriteLine("This is likely an EMV (bank) chip card");
                            using (var isoReader = new IsoReader(context, readerName, SCardShareMode.Shared, SCardProtocol.Any, false))
                            {
                                ListApplications(isoReader);
                            }
                            if (atr.Length >= 13 && atr[10] == 0x41 && atr[11] == 0x53 && atr[12] == 0x4C)
                            {
                                Console.WriteLine("Possibly manufactured by Athena Smart Card Solutions");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Unknown smart card type");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unknown card type");
                    }
                }
            }
            catch (PCSCException ex)
            {
                Console.WriteLine($"Error determining card type: {ex.Message}");
            }
        }

        static void ListApplications(IsoReader isoReader)
        {
            // List of known AIDs for major brands
            var knownAIDs = new List<byte[]>
            {
                new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x03, 0x10, 0x10 }, // Visa
                new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x04, 0x10, 0x10 }, // MasterCard
                new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x25, 0x01 },       // American Express
                new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x65, 0x10, 0x10 }, // Discover
                new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x30, 0x60, 0x00 }, // JCB
            };

            foreach (var aid in knownAIDs)
            {
                try
                {
                    var selectApp = new CommandApdu(IsoCase.Case4Short, isoReader.ActiveProtocol)
                    {
                        CLA = 0x00,
                        INS = 0xA4,
                        P1 = 0x04,
                        P2 = 0x00,
                        Data = aid,
                        Le = 0
                    };

                    var response = isoReader.Transmit(selectApp);

                    if (response.SW1 == 0x90 && response.SW2 == 0x00)
                    {
                        Console.WriteLine("Application selected successfully.");
                        Console.WriteLine($"Selected AID: {BitConverter.ToString(aid)}");
                        // Now proceed with GPO and reading records if needed
                        ReadEmvCardData(isoReader);
                        break;
                    }
                }
                catch (PCSCException ex)
                {
                    Console.WriteLine($"Error selecting AID {BitConverter.ToString(aid)}: {ex.Message}");
                }
            }
        }

        static void ReadEmvCardData(IsoReader isoReader)
        {
            try
            {
                // Get Processing Options
                var getProcessingOptions = new CommandApdu(IsoCase.Case4Short, isoReader.ActiveProtocol)
                {
                    CLA = 0x80,
                    INS = 0xA8,
                    P1 = 0x00,
                    P2 = 0x00,
                    Data = new byte[] { 0x83, 0x00 }, // Empty PDOL
                    Le = 0
                };

                var gpoResponse = isoReader.Transmit(getProcessingOptions);
                if (!gpoResponse.HasData)
                {
                    Console.WriteLine("Failed to get processing options.");
                    Console.WriteLine($"Status Word: {gpoResponse.SW1:X2} {gpoResponse.SW2:X2}");
                    return;
                }

                Console.WriteLine("Processing options received successfully.");

                // Flags to ensure data is only displayed once
                bool panDisplayed = false;
                bool expDateDisplayed = false;
                bool nameDisplayed = false;

                // Read the application data records
                for (byte sfi = 1; sfi <= 10; sfi++)
                {
                    for (byte record = 1; record <= 10; record++)
                    {
                        var readRecord = new CommandApdu(IsoCase.Case2Short, isoReader.ActiveProtocol)
                        {
                            CLA = 0x00,
                            INS = 0xB2,
                            P1 = record,
                            P2 = (byte)((sfi << 3) | 0x04),
                            Le = 0
                        };

                        var readResponse = isoReader.Transmit(readRecord);

                        if (readResponse.HasData)
                        {
                            ExtractAndDisplayRelevantData(readResponse.GetData(), ref panDisplayed, ref expDateDisplayed, ref nameDisplayed);
                        }
                        else if (readResponse.SW1 == 0x6A && readResponse.SW2 == 0x83)
                        {
                            // SW1-SW2 = 6A83 means 'Record not found'
                            break;
                        }

                        // Exit the loop if all data has been displayed
                        if (panDisplayed && expDateDisplayed && nameDisplayed)
                        {
                            return;
                        }
                    }
                }
            }
            catch (PCSCException ex)
            {
                Console.WriteLine($"Error reading card: {ex.Message}");
            }
        }

        static void ExtractAndDisplayRelevantData(byte[] data, ref bool panDisplayed, ref bool expDateDisplayed, ref bool nameDisplayed)
        {
            // Display only records with relevant information
            for (int i = 0; i < data.Length - 4; i++)
            {
                if (!panDisplayed && data[i] == 0x5A) // Tag for PAN in EMV
                {
                    int length = data[i + 1];
                    byte[] panData = new byte[length];
                    Array.Copy(data, i + 2, panData, 0, length);
                    Console.WriteLine($"Card Number (PAN): {BitConverter.ToString(panData).Replace("-", "")}");
                    panDisplayed = true;
                }
                else if (!expDateDisplayed && data[i] == 0x5F && data[i + 1] == 0x24) // Tag for expiration date
                {
                    int length = data[i + 2];
                    byte[] dateData = new byte[length];
                    Array.Copy(data, i + 3, dateData, 0, length);
                    Console.WriteLine($"Expiration Date (YYMM): {BitConverter.ToString(dateData).Replace("-", "")}");
                    expDateDisplayed = true;
                }
                else if (!nameDisplayed && data[i] == 0x5F && data[i + 1] == 0x20) // Tag for cardholder name
                {
                    int length = data[i + 2];
                    byte[] nameData = new byte[length];
                    Array.Copy(data, i + 3, nameData, 0, length);
                    Console.WriteLine($"Cardholder Name: {System.Text.Encoding.ASCII.GetString(nameData)}");
                    nameDisplayed = true;
                }

                // Exit the loop if all data has been displayed
                if (panDisplayed && expDateDisplayed && nameDisplayed)
                {
                    break;
                }
            }
        }
    }
}