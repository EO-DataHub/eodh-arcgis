# EODH Plugin for ArcGIS Pro — Usage Guide

The EODH plugin brings the [Earth Observation DataHub](https://eodatahub.org.uk) into ArcGIS Pro, letting you search, preview, and load satellite imagery directly into your map. It also supports purchasing commercial imagery from Airbus and Planet.

## Requirements

- Windows 10/11 (x64)
- ArcGIS Pro 3.6 or later
- An EODH account with an API token

## Installation

1. Download the latest `eodh.esriAddinX` file from the [GitHub Releases](https://github.com/EO-DataHub/eodh-arcgis/releases) page.
2. Double-click the downloaded file.
3. Click **Install Add-In** when prompted.
4. Restart ArcGIS Pro if it was already open.

After installation, a new **EODH** tab appears in the ArcGIS Pro ribbon.

![EODH tab in ArcGIS Pro ribbon](Images/ribbon.png)

## Getting Started

### Signing In

1. Click the **EODH Search** button in the EODH ribbon tab to open the dockpane.
2. Select your environment (Production, Staging, or Test). If you're not sure, select Production.
3. Enter your workspace name and API token.
4. Click **Sign In**.

Your credentials are encrypted and stored locally, so you won't need to sign in again next time.

![Login view](Images/login.png)

### Signing Out

Click **Sign Out** in the header bar at the top of the dockpane.

## Searching for Data

The **Search** tab lets you define filters and query the EODH catalog.

![Search tab](Images/search.png)

### 1. Select a Catalog and Collection

Use the dropdown menus at the top of the Search tab to choose a catalog and, optionally, a specific collection within it.

### 2. Define an Area of Interest (AOI)

Choose one of four methods:

| Method | Description |
|--------|-------------|
| **Draw on Map** | Click the button, then draw a rectangle on the map canvas |
| **Map Extent** | Use the current map view as the AOI |
| **Import GeoJSON** | Load a boundary from a `.geojson` or `.json` file |
| **Clear** | Remove the current AOI |

![Drawing an AOI on the map](Images/aoi.png)

### 3. Set Date Range

Use the date pickers to define the start and end dates for your search. By default, this covers the last two months.

### 4. Set Max Cloud Cover

Drag the slider to set a cloud cover threshold (0–100%). Only results at or below this value will be returned.

### 5. Run the Search

Click **Search**. A summary will show how many items were found.

## Browsing Results

Results appear in the **Results** tab.

![Results tab with timeline](Images/results.png)

### Timeline

A scrollable timeline strip at the top shows thumbnail previews of each result arranged by acquisition date. Use the **Previous** / **Next** buttons or scroll to navigate. Clicking a thumbnail highlights the corresponding item in the list below.

### Results List

Each result displays:

- **Thumbnail** preview
- **Item ID** and **Collection**
- **Acquisition date**
- **Resolution** (metres)
- **Cloud cover** (%)
- **AOI overlap** (%)

### Selecting Assets

Click on a result to expand its asset list. Each asset shows its type (COG, GeoTIFF, etc.) and whether it can be loaded. Use the checkboxes to select the assets you want, then click **Load Selected Assets**.

You can also **double-click** a result to load its selected assets directly into the map.

![Expanded asset list for a result](Images/assets.png)

## Purchasing Commercial Imagery

Commercial items are highlighted with a gold border. The purchase workflow depends on the provider:

| Provider | Licence Required | Country Required |
|----------|:----------------:|:----------------:|
| Airbus Optical | Yes | Yes (ISO-3 code) |
| Airbus SAR | Yes | No |
| Planet | No | No |

### Purchase Steps

1. Select a licence from the dropdown (if required).
2. Enter the end-user country code (if required).
3. Click **Get Quote** to see the price.
4. Review the price and currency displayed.
5. Click **Place Order** and confirm the purchase.

You can track order status in the **Workspace** tab.

![Commercial item with quote](Images/commercial.png)
