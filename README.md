# Smart Card Reader Test Application

The Smart Card Reader Test Application monitors connected smart card readers and detects when a card is inserted or removed. It provides real-time notifications for these events, ensuring users are aware of card interactions.

## Product Information

The smart card reader being tested is the ZOWEETEK Smart Card Reader. It is a USB-connected smart card reader with features including:

- **Driver-Free Installation:** The CAC reader is compatible with Windows, Linux, and Mac OS. It is driver-free, providing a plug-and-play experience.
- **ID/IC Strong Compatibility:** Supports Government ID, ActivClient, AKO, OWA, DKO, JKO, NKO, BOL, GKO, Marinenet, AF Portal, Pure Edge Viewer, ApproveIt, DCO, DTS, LPS, Disa Enterprise Email, and other CAC chip cards.
- **Convenience:** No card reader driver is needed, which shortens operating time. It offers convenience, security, and reliability.
- **Mac Users:** Java is necessary for Mac users. Please install Java from Java's official website.
- **Certifications:** PIV, EMS, ISO-7816 & EMV2 2000 Level 1, CE, FCC, VCCI, and Microsoft WHQL certifications.
- **USB Port:** USB A
- **PC Compatibility:** Windows, Linux, Mac OS
- **Long Service Life:** Equipped with high-end chips for a long service life.

For more details about the Smart Card Reader, visit the [Amazon product page](https://www.amazon.com/dp/B09WK9NJQ4?th=1).

## Application Features

- **Card Detection:** The application detects and notifies users of card insertions and removals.
- **ATR Retrieval:** Upon card insertion, the application retrieves and analyzes the Answer To Reset (ATR) from the card. This helps identify the card type and compatibility, distinguishing between various card technologies like EMV, MIFARE, and JavaCard.
- **Application Selection:** The application scans the card for known Application Identifiers (AIDs) associated with major brands (e.g., Visa, MasterCard, American Express) and attempts to select the appropriate application to access card data.
- **Data Extraction:** Reads data records from the card to extract key information, including:
  - **Primary Account Number (PAN)**
  - **Expiration Date** (YYMM format)
  - **Cardholder Name**
- **Information Filtering:** The application filters and displays only relevant information, ensuring that duplicate or unnecessary data is not shown. Each piece of data (PAN, expiration date, and cardholder name) is extracted and displayed only once per session.
- **Error Handling:** Comprehensive error handling mechanisms manage exceptions during card reading, application selection, and data retrieval processes. Users are informed of errors through console messages, ensuring transparency in operations.
