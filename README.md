# 📊 PoForecasting

Time-series price forecasting system built with **.NET 9** + **ML.NET (SSA)** using **Clean Architecture**.

## 🚀 Overview
Predicts future `PRICE_PER_UNIT` for purchase order part codes by leveraging:

*   **📦 Historical Depth:** 10+ years of Purchase Order data.
*   **📈 Macro Indicators:** Consumer Price Index ([CPIAUCSL](https://fred.stlouisfed.org)) integration.
*   **🧠 Advanced Analytics:** SSA (Singular Spectrum Analysis) time-series forecasting.
*   **💰 Economic Precision:** Inflation-adjusted modeling (real → nominal reconstruction).

## 🎯 Purpose

To estimate the future purchase price of specific part codes for budgeting and procurement planning.

### ⚙️ How it Works

The system executes a multi-stage forecasting pipeline:

1.  **Data Aggregation:** Consolidates raw PO data into monthly average prices.
2.  **CPI Alignment:** Synchronizes historical data with [CPIAUCSL](https://fred.stlouisfed.org/series/CPIAUCSL) indices by Year and Month.
3.  **Inflation Normalization:** Converts nominal prices into **real prices** to remove inflationary noise.
4.  **Dual-Stream Forecasting:** 
    *   Forecasts **real price** trends using SSA.
    *   Forecasts future **CPI values** using SSA.
5.  **Reconstruction:** Merges both forecasts to reconstruct the future **nominal price**.
6.  **Output:** Returns actionable price estimates for procurement forecasting.


## 🏗 Architecture

The project follows **Clean Architecture** principles to ensure separation of concerns and testability:

```text
PoForecasting
│
├── Core
│   ├── Domain         # Models and DTOs
│   ├── Abstractions   # Repository and Service interfaces
│   └── Services       # Logic (PriceForecastService, SsaForecaster, etc.)
│
├── Infrastructure
│   └── Csv            # CSV-based repository implementations
│
├── ConsoleForeCaster
│   └── Full diagnostics, training metrics, and model details
│
└── ConsoleForeCasterSimple
    └── Executive view (clean price output only)


## 📂 Data Requirements

### 1️⃣ Purchase Orders CSV
The system requires a historical transaction file to train the SSA model. While the source may contain various metadata, specific fields are mandatory for the forecasting pipeline.

**Required Schema:**

| Column | Description | Role |
| :--- | :--- | :--- |
| `ORDER_DATE` | Date of the transaction | Temporal grouping |
| `PART_CODE` | Unique identifier for the part | Filtering/Grouping |
| `PRICE_PER_UNIT` | Nominal price at time of purchase | Target Variable |
| `SYS_CURRENCY_CODE` | Currency (e.g., USD) | Validation |

**CSV Example:**
```csv
PO_NUMBER,ORDER_DATE,THEYEAR,THEMONTH,PART_CODE,IC_NOMINATED_QTY,IC_NOMINATED_UNIT,PRICE_PER_UNIT,TOTAL_ORDER_VALUE,SYS_CURRENCY_CODE
210328,2/1/2016,2016,2,888012,360,EA,20.4656,7367.6,USD


### 2️⃣ CPI CSV
A secondary dataset containing the Consumer Price Index for All Urban Consumers ([CPIAUCSL](https://fred.stlouisfed.org)). This is utilized to calculate **Real Price** and perform inflation-adjusted forecasting.

**Required Schema:**


| Column | Format | Description |
| :--- | :--- | :--- |
| `observation_date` | `YYYY-MM-DD` | The monthly index date |
| `CPIAUCSL` | `Decimal` | The index value for that month |

**CSV Example:**
```csv
observation_date,CPIAUCSL
2016-01-01,237.652
2016-02-01,237.336


## 🧠 Forecasting Methodology

### Step 1 — Monthly Aggregation
The system reduces noise from high-frequency transaction data by normalizing all PO lines into a uniform monthly time series.

*   **Logic:** Groups all transactions by part code and month.
*   **Transformation:** 
    `Multiple PO Lines` $\rightarrow$ `yyyy-MM-01` $\rightarrow$ **Average `PRICE_PER_UNIT`**

This creates the baseline series used for the subsequent **SSA decomposition**.

### Step 2 — CPI Alignment
The aggregated PO data is joined with the CPI dataset based on the **Year + Month** of the transaction.

### Step 3 — Convert Nominal → Real
To isolate the true price trend from inflationary noise, nominal prices are converted to **Real Prices** using the following formula:

$$\text{real\_price} = \text{nominal} \times \left( \frac{\text{Base CPI}}{\text{Month CPI}} \right)$$

*   **Base CPI:** The CPI value of the **last training month** in the series.

### Step 4 — Forecast Real Price (SSA)
The system applies the [ML.NET SSA Forecasting](https://learn.microsoft.com) trainer to the real price series using these parameters:

*   **Window Size:** `12` (Captures yearly seasonality)
*   **Series Length:** `24`
*   **Confidence Level:** `95%`
*   **Log Transformation:** `Enabled` (To stabilize variance)

### Step 5 — Forecast CPI
A separate **SSA model** is trained on historical CPI data to predict the inflationary environment for the forecast horizon.

### Step 6 — Reconstruct Nominal
The final expected purchase price is reconstructed by applying the predicted inflation back to the real price forecast:

$$\text{nominal} = \text{real} \times \left( \frac{\text{CPI\_future}}{\text{Base CPI}} \right)$$

This yields the **Final Nominal Forecast**—the price you should expect to see on a future Purchase Order.

## 🖥 Console Applications

### 1️⃣ ConsoleForeCaster (Detailed Version)
Designed for data analysts, this version provides full transparency into the model's internal logic.

**Outputs:**
*   **Forecasts:** Nominal, Real, and CPI predictions.
*   **Statistics:** Confidence intervals and data coverage diagnostics.
*   **Metadata:** SSA parameters and training configurations.

**Example Usage:**
```bash
dotnet run -- \
  --po "E:\data\po.csv" \
  --cpi "E:\data\cpi.csv" \
  --part 888012 \
  --months 6 \
  --currency USD \
  --log true \
  --confidence 0.95


## 2️⃣ ConsoleForeCasterSimple (Business View)

Minimal output.

### Defaults:
* **Currency:** `USD`
* **Log transform:** `true`
* **Confidence:** `0.95`

### Important Behavior
`--months 6` means: Forecast 6 months ahead of the current month, not 6 months after the last training month.

### Example Usage
```bash
dotnet run --project src/ConsoleForeCasterSimple -- \
  --po "E:\data\po.csv" \
  --cpi "E:\data\cpi.csv" \
  --part 888012 \
  --months 6


---- Forecast Price for Part Code 888012 (Nominal PRICE_PER_UNIT + Expected Inflation) ----
2026-03-01  Price Prediction=10.9216
2026-04-01  Price Prediction=10.7997
2026-05-01  Price Prediction=10.7487
2026-06-01  Price Prediction=10.6770
2026-07-01  Price Prediction=10.5389
2026-08-01  Price Prediction=10.4591

### 💰 Which Price Should Be Used?


| Use Case | Choose |
| :--- | :--- |
* **Budgeting / PO planning**: Nominal
* **Supplier inflation analysis**: Real
* **Risk planning**: Upper 95% nominal bound


### 🔧 Current Defaults


| Parameter | Value |
| :--- | :--- |
| **Log Transform** | Enabled |
| **Confidence Level** | 95% |
| **Seasonality Window** | 12 months |
| **Series Length** | 24 |
| **Minimum Monthly Points** | 24 |


### 🚀 Future Roadmap

**Planned enhancements:**
* [ ] **ASP.NET Core API layer**
* [ ] **Oracle repository implementation**
* [ ] **Backtesting & model validation**
* [ ] **Automatic hyperparameter tuning**
* [ ] **Model persistence**
* [ ] **Multi-part batch forecasting**
* [ ] **Confidence reporting for executive dashboards**


### ⚠️ Notes on Accuracy

**Forecast accuracy depends on:**
*   **Recency of PO data:** Data should be as current as possible.
*   **Stability of supplier pricing:** Sudden price hikes can skew results.
*   **Inflation regime changes:** Unexpected shifts in economic policy or conditions.
*   **Forecast horizon length:** Accuracy decreases as the projection moves further into the future.

> [!IMPORTANT]
> Forecasting many years beyond the last training month significantly reduces model reliability.


### 🧾 Tech Stack

*   **.NET 9**
*   **ML.NET**
*   **SSA** (Singular Spectrum Analysis)
*   **Clean Architecture**
*   **CSV** (initially)
*   **Oracle** (planned)


### 📌 Summary

**PoForecasting** provides a structured, inflation-aware, time-series forecasting solution for procurement planning.

It separates concerns into:
* **Core modeling logic**
* **Data access layer**
* **Presentation layer** (Console / future API)

This modular design allows for a seamless transition to:
* **ASP.NET Core API**
* **Database-backed repositories**
* **Enterprise integration**
