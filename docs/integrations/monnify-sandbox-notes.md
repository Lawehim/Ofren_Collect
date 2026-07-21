# Monnify Sandbox Integration Notes

Integration reference for a .NET backend integrating with the **Monnify sandbox** for a
recurring-billing reconciliation system. Contract details below were taken from the official
Monnify documentation. Anything not directly confirmable from official docs is called out in the
**Confidence / unconfirmed** section at the bottom.

> Last researched: 2026-07-19.
> Monnify docs are actively being migrated between a Confluence wiki (`teamapt.atlassian.net`),
> the current developer portal (`developers.monnify.com`), and a newer playground
> (`monnify-docs.playground.monnify.com`). Where two doc generations disagree, both are noted.

## Source URLs consulted

- Authentication — https://teamapt.atlassian.net/wiki/spaces/MON/pages/212008633/Authentication
- Reserve an Account (V2) — https://teamapt.atlassian.net/wiki/spaces/MON/pages/289046549/Reserve+An+Account+V2
- Customer Reserved Account (dev portal) — https://developers.monnify.com/docs/collections/customer-reserved-account
- Get Transaction Status — https://teamapt.atlassian.net/wiki/spaces/MON/pages/213909851/Get+Transaction+Status
- Verify Transactions (dev portal) — https://developers.monnify.com/docs/collections/manage-payments/verify-transactions
- Transaction Completion Webhook — https://teamapt.atlassian.net/wiki/spaces/MON/pages/213909300/Transaction+Completion+Webhook
- Calculating the Transaction Hash / Computing Request Validation Hash — https://teamapt.atlassian.net/wiki/spaces/MON/pages/212008918/Calculating+the+Transaction+Hash
- Webhooks (dev portal) — https://developers.monnify.com/docs/integration-tools/webhooks/
- Webhooks (playground) — https://monnify-docs.playground.monnify.com/docs/webhooks
- Webhook Event Types — https://developers.monnify.com/docs/webhooks/event-types
- Single Transfers — https://developers.monnify.com/docs/disbursements/single-transfers
- Initiate Transfers — https://teamapt.atlassian.net/wiki/spaces/MON/pages/223149917/Initiate+Transfers
- Testing Pay with Transfer on Monnify Sandbox — https://developers.monnify.com/blog/testing-pay-with-transfer-on-monnify-sandbox
- Integration Tools (Banking App Simulator) — https://teamapt.atlassian.net/wiki/spaces/MON/pages/213909537/Integration+Tools
- Webhook Events and Request Structure (verbatim SUCCESSFUL_TRANSACTION payload) — https://teamapt.atlassian.net/wiki/spaces/MON/pages/320864320

---

## Sandbox base URL and test credentials

- **Sandbox base URL:** `https://sandbox.monnify.com`
- **Live base URL (for reference, do not use in dev):** `https://api.monnify.com`
- All example endpoints below use `{{base_url}}` = the sandbox URL above.
- **Credentials** — you supply three values from your Monnify dashboard (**Developers** section):
  - `API Key`
  - `Secret Key` (this is the **client secret** used both for Basic Auth and for webhook hash verification)
  - `Contract Code`
- Real keys come from **your own Monnify dashboard**. Do NOT commit real API keys, secret keys, or
  contract codes to source control. Store them in user-secrets / environment variables / a vault.
  No real secrets are included in this document.

---

## 1. Authentication (obtain access token)

Monnify uses OAuth2 Bearer tokens. You first exchange your API key + secret key (HTTP Basic auth)
for a short-lived access token, then send that token as a Bearer token on every other call.

- **Endpoint:** `POST {{base_url}}/api/v1/auth/login`
- **Auth scheme:** HTTP Basic. Build the header value as
  `Basic ` + Base64( `<API_KEY>` + `:` + `<SECRET_KEY>` ).
- **Request body:** none required (empty body).

### Request

```
POST https://sandbox.monnify.com/api/v1/auth/login
Authorization: Basic <base64(apiKey:secretKey)>
Content-Type: application/json
```

### Response

```json
{
  "requestSuccessful": true,
  "responseMessage": "success",
  "responseCode": "0",
  "responseBody": {
    "accessToken": "<JWT-access-token>",
    "expiresIn": 3599
  }
}
```

**Notes**

- The token lives in `responseBody.accessToken`.
- `expiresIn` is in **seconds** (~3599 = ~1 hour).
- Cache the token and reuse it until near expiry; refresh proactively (e.g. when < 60s remaining)
  or on a `401`. Re-authenticating on every request is unnecessary and rate-limit-unfriendly.
- Every non-auth endpoint below requires `Authorization: Bearer <accessToken>`.
- The envelope pattern (`requestSuccessful` / `responseMessage` / `responseCode` / `responseBody`)
  is used by **all** Monnify JSON responses. `responseCode` `"0"` = success.

---

## 2. Reserved (virtual) account creation

Creates a dedicated bank account (or set of accounts across partner banks) that a customer pays
into. Perfect for a per-subscription reserved account: use a stable `accountReference` you control
(e.g. your subscription id) so incoming webhooks map back to the subscription.

- **Endpoint:** `POST {{base_url}}/api/v2/bank-transfer/reserved-accounts`
- **Auth:** `Authorization: Bearer <accessToken>`
- Set `getAllAvailableBanks: true` to reserve accounts across all partner banks, **or** set it to
  `false` and pass `preferredBanks` (array of bank codes) to restrict to chosen banks.
- The API accepts BVN and/or NIN. Both linked → maximum transaction limits; only one → limited
  limits. (`bvn`/`nin` may be optional depending on your contract's limit configuration.)

### Request

```json
{
  "accountReference": "abc123",
  "accountName": "Test Reserved Account",
  "currencyCode": "NGN",
  "contractCode": "<YOUR_CONTRACT_CODE>",
  "customerEmail": "test@tester.com",
  "customerName": "John Doe",
  "bvn": "21212121212",
  "nin": "12034875601",
  "getAllAvailableBanks": true,
  "incomeSplitConfig": [
    {
      "subAccountCode": "MFY_SUB_319452883228",
      "feePercentage": 10.5,
      "splitPercentage": 20,
      "feeBearer": true
    }
  ],
  "restrictPaymentSource": true,
  "allowedPaymentSources": {
    "bvns": ["21212121212", "20202020202"],
    "bankAccounts": [
      { "accountNumber": "0068687503", "bankCode": "232" }
    ],
    "accountNames": ["SAMUEL DAMILARE OGUNNAIKE"]
  }
}
```

To restrict to preferred banks instead of all banks, replace `getAllAvailableBanks: true` with:

```json
  "getAllAvailableBanks": false,
  "preferredBanks": ["035", "50515"]
```

`incomeSplitConfig`, `restrictPaymentSource`, and `allowedPaymentSources` are optional — include
them only if you need split settlement or source restriction.

### Response

```json
{
  "requestSuccessful": true,
  "responseMessage": "success",
  "responseCode": "0",
  "responseBody": {
    "contractCode": "<YOUR_CONTRACT_CODE>",
    "accountReference": "abc1234",
    "accountName": "Tes",
    "currencyCode": "NGN",
    "customerEmail": "test@tester.com",
    "customerName": "John Doe",
    "accounts": [
      {
        "bankCode": "50515",
        "bankName": "Moniepoint Microfinance Bank",
        "accountNumber": "6254727989",
        "accountName": "Tes"
      }
    ],
    "collectionChannel": "RESERVED_ACCOUNT",
    "reservationReference": "NWA7DMJ0W2UDK1KN5SLF",
    "reservedAccountType": "GENERAL",
    "status": "ACTIVE",
    "createdOn": "2023-04-14 12:04:39.034",
    "incomeSplitConfig": [],
    "bvn": "21212121212",
    "nin": "12034875601",
    "restrictPaymentSource": false
  }
}
```

**Notes**

- `responseBody.accounts` is an **array** — with `getAllAvailableBanks: true` you get one entry per
  partner bank (each its own `accountNumber` + `bankCode`/`bankName`). Persist all of them; a
  customer may pay into any one.
- `reservationReference` is Monnify's server-side id for the reservation; `accountReference` is the
  echo of the value **you** supplied and is your correlation key.
- `status: "ACTIVE"` means the account can receive payments.

---

## 3. Transaction verification (server-side confirmation)

Always independently query Monnify for the true status of a transaction before crediting anything —
never trust a webhook/callback alone.

There are two documented verification endpoints across the doc generations. Prefer the one your
contract's docs show; both are listed.

### 3a. Get Transaction Status (v2, by Monnify transactionReference)

- **Endpoint:** `GET {{base_url}}/api/v2/transactions/{transactionReference}`
- **Auth:** `Authorization: Bearer <accessToken>`
- **URL-encode** the `transactionReference` before inserting it into the path — Monnify references
  contain `|` characters (e.g. `MNFY|20200226093601|002095`).

Response:

```json
{
  "requestSuccessful": true,
  "responseMessage": "success",
  "responseCode": "0",
  "responseBody": {
    "transactionReference": "MNFY|20200226093601|002095",
    "paymentReference": "330854835",
    "amountPaid": "100.00",
    "totalPayable": "100.00",
    "paidOn": "26/02/2020 09:38:13 AM",
    "paymentStatus": "PAID",
    "currency": "NGN",
    "accountDetails": {
      "accountName": "DAMILARE SAMUEL OGUNNAIKE",
      "accountNumber": "******7503",
      "bankCode": "000001",
      "amountPaid": "100.00"
    }
  }
}
```

### 3b. Verify by reference (v1, dev-portal variant)

- **Endpoint:** `GET {{base_url}}/api/v1/transactions/verify_by_reference?reference={reference}`
  where `{reference}` can be either your `paymentReference` or Monnify's `transactionReference`.
- Documented richer fields: `paymentStatus`, `amountPaid`, `totalPayable`, `settlementAmount`,
  `paymentMethod`, and `paymentSourceInformation` (payer account name/number, useful for
  reconciliation).

**How to read the key fields (both variants)**

- **Paid amount:** `responseBody.amountPaid` (string, e.g. `"100.00"`). Compare against expected.
- **Amount charged to customer:** `responseBody.totalPayable`.
- **Payment status:** `responseBody.paymentStatus`. Only treat `PAID` (and, per your rules,
  `OVERPAID`) as fully successful. Full status set: `PAID`, `OVERPAID`, `PARTIALLY_PAID`,
  `PENDING`, `ABANDONED`, `CANCELLED`, `FAILED`, `REVERSED`, `EXPIRED`.
- **Account paid into:** `responseBody.accountDetails.accountNumber` / `bankCode` (v2). Note the
  account number may be **masked** (e.g. `******7503`).
- **Paid-on timestamp:** `responseBody.paidOn`, format `dd/MM/yyyy hh:mm:ss a` (12-hour + AM/PM),
  e.g. `"26/02/2020 09:38:13 AM"`. Note: NOT ISO-8601 — parse with an explicit format.

---

## 4. Webhook / transaction notification + signature verification

When money lands in a reserved account, Monnify POSTs a notification to your configured webhook URL.

- **HTTP method:** `POST` to your configured webhook URL (JSON body).
- **You must respond `200 OK`** to acknowledge. Do heavy processing after acknowledging.

### 4a. Signature verification (SECURITY-CRITICAL)

- **Header:** `monnify-signature`
- **Algorithm:** **HMAC-SHA512**
- **HMAC key:** your **client Secret Key** (the same secret key from the dashboard used for Basic
  Auth in section 1).
- **Data hashed:** the **raw HTTP request body** exactly as received (the raw bytes/string — do
  NOT re-serialize the parsed JSON, as re-serialization can change spacing/ordering and break the
  hash). Monnify describes it as "SHA-512 of client secret key + object of request body", which in
  practice is `HMAC-SHA512(key = secretKey, message = rawRequestBody)`.
- **Output encoding:** lowercase **hex** string.
- Compare using a **constant-time** comparison (e.g. `CryptographicOperations.FixedTimeEquals` in
  .NET) — not `==` — to avoid timing attacks.

Reference implementations from the docs:

```javascript
// Node.js (sha512 lib)
const result = sha512.hmac(DEFAULT_MERCHANT_CLIENT_SECRET, requestBody);
// compare result === req.headers['monnify-signature']
```

```php
// PHP / Laravel
$computed = hash_hmac('sha512', $request->getContent(), config('monnify.secret_key'));
if (hash_equals($computed, $request->header('monnify-signature'))) { /* valid */ }
```

.NET equivalent (illustrative — read the raw body before model binding):

```csharp
// rawBody must be the exact bytes/string received, NOT re-serialized
using var hmac = new System.Security.Cryptography.HMACSHA512(
    System.Text.Encoding.UTF8.GetBytes(secretKey));
var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawBody));
var computed = Convert.ToHexString(hashBytes).ToLowerInvariant();
var provided = request.Headers["monnify-signature"].ToString();
bool valid = CryptographicOperations.FixedTimeEquals(
    System.Text.Encoding.UTF8.GetBytes(computed),
    System.Text.Encoding.UTF8.GetBytes(provided));
```

### 4b. Webhook payload shape

Monnify has two payload generations. Confirm which your account sends by inspecting real sandbox
deliveries.

**Legacy / "Transaction Completion Webhook" (flat payload — confirmed verbatim):**

```json
{
  "transactionReference": "MNFY|20200900003149|000000",
  "paymentReference": "MNFY|20200900003149|000000",
  "amountPaid": "180000.00",
  "totalPayable": "180000.00",
  "paymentStatus": "PAID",
  "paidOn": "09/09/2020 11:31:56 AM",
  "transactionHash": "<hash value>",
  "paymentMethod": "ACCOUNT_TRANSFER"
}
```

In this legacy form the body itself carries a `transactionHash` field (a hash Monnify computed over
the body) — but the authoritative signature to verify is still the `monnify-signature` header.

**Current / event-wrapped payload (structure — see unconfirmed note):** newer notifications wrap
the data in an envelope:

```json
{
  "eventType": "SUCCESSFUL_TRANSACTION",
  "eventData": {
    "transactionReference": "MNFY|...",
    "paymentReference": "...",
    "amountPaid": "180000.00",
    "totalPayable": "180000.00",
    "paymentStatus": "PAID",
    "paidOn": "09/09/2020 11:31:56 AM",
    "paymentMethod": "ACCOUNT_TRANSFER",
    "currency": "NGN",
    "customer": { "email": "...", "name": "..." },
    "destinationAccountInformation": {
      "bankCode": "...",
      "bankName": "...",
      "accountNumber": "..."
    },
    "product": { "reference": "...", "type": "RESERVED_ACCOUNT" }
  }
}
```

**Notes / best practices from the docs**

- After verifying the signature, **re-verify server-side** (section 3) before trusting the amount
  and status.
- Guard against **duplicate** notifications (idempotency on `transactionReference`).
- Optionally whitelist Monnify's IP.
- The account the money landed in appears (current payload) under
  `eventData.destinationAccountInformation`; map it to your subscription via the reserved account's
  `accountReference`/`product.reference`.

---

## 5. Transfers / disbursement (single transfer)

Used for a stretch "settlement" feature — pay out from your Monnify wallet to a bank account.

- **Endpoint:** `POST {{base_url}}/api/v2/disbursements/single`
- **Auth:** `Authorization: Bearer <accessToken>`

### Request

```json
{
  "amount": 500.00,
  "reference": "unique-ref-002",
  "narration": "Payment from Acme Ltd",
  "destinationBankCode": "058",
  "destinationAccountNumber": "0123456789",
  "destinationAccountName": "John Doe",
  "currency": "NGN",
  "sourceAccountNumber": "9999999999"
}
```

- Optional `"async": true` processes the transfer asynchronously and returns a pending status; the
  final status then arrives via a disbursement webhook.
- Disbursements are typically protected by a **Transaction PIN / 2FA (OTP)** on the merchant
  account depending on configuration (see "Initiate Transfers" doc).

### Response

```json
{
  "requestSuccessful": true,
  "responseMessage": "success",
  "responseCode": "0",
  "responseBody": {
    "availableBalance": 24500.00,
    "ledgerBalance": 25000.00,
    "accountNumber": "9999999999",
    "currency": "NGN"
  }
}
```

> Note: the sample response above (from the dev-portal Single Transfers page) shows a balance-style
> body. Other docs show a disbursement response including `reference`, `status`
> (e.g. `SUCCESS` / `PENDING`), `amount`, `transactionReference`, and `dateCreated`. Confirm the
> exact shape against your contract before relying on specific fields.

---

## Simulating a sandbox payment into a reserved account

Real bank transfers do not work in the Monnify sandbox. To make money "land" in a reserved
account you use Monnify's **Banking App Simulator** (a fake mobile-banking-app UI), which tells
the sandbox to mark a matching transaction as `PAID` and fire the collection webhook. There is
**no dashboard "fund test account" button and no REST endpoint that credits a reserved account** —
the simulator is the mechanism.

### The simulator

- **URL:** `https://websim.sdk.monnify.com/#/bankingapp`
  (the blog writes it as `https://websim.sdk.monnify.com/?#/bankingapp` — same page).
- Per the Confluence *Integration Tools* page, the simulator "allows you to simulate a transfer to
  **any of the virtual accounts you created in the sandbox environment**." A reserved (virtual)
  account is exactly such an account, so you can pay into it directly.
- The UI resembles a Nigerian mobile banking app. Use the **Transfer** option in its nav.

### Fields to enter in the simulator (verbatim from the "Testing Pay with Transfer" blog)

1. **Bank** — "Select the same bank shown on your checkout page." For a reserved account, select
   the bank the reserved account was issued on (for account `4003115967` that is **Wema bank**).
2. **Account number** — "Paste the virtual account number displayed to the customer" — i.e. the
   reserved account number (`4003115967`).
3. **Amount** — "Type the _exact_ amount specified when the transaction was initialized." Monnify
   matches on account number **and** amount; the wrong amount yields `OVERPAID`/`PARTIALLY_PAID`.
4. Click **Make Payment**. "The simulator notifies Monnify's sandbox infrastructure, which marks
   the transaction as `PAID` and dispatches the webhook."

### Reserved account vs one-time-payment: important distinction

- The "Testing Pay with Transfer" blog is written for a **one-time Pay-with-Transfer** flow, where
  you first `Initialize Transaction` + `bank-transfer/init-payment` to mint a **temporary** account
  and a `transactionReference`, then simulate paying that temporary account.
- A **reserved account is a permanent, standing account** — it already exists (you created it via
  `POST /api/v2/bank-transfer/reserved-accounts`). You do **not** need to initialize a transaction
  first. Per the Integration Tools page you simulate a transfer straight to the reserved account
  number, and Monnify generates a brand-new transaction for that credit. **The
  `transactionReference` therefore does NOT exist before you pay — Monnify mints it for the
  simulated inflow and delivers it in the webhook** (and via the reserved-account transactions
  list / verify endpoints afterward). This is the key difference from the one-time flow, where you
  hold the reference up-front.

### Obtaining the transactionReference of the simulated inflow

Because the reference is server-generated at payment time, get it from one of:

- **The webhook** (primary): `eventData.transactionReference` (see JSON below).
- **The sandbox dashboard:** Transactions list — the new PAID transaction appears with its
  reference.
- **Reserved-account transactions API:**
  `GET {{base_url}}/api/v1/bank-transfer/reserved-accounts/transactions?accountReference={yourRef}`
  lists transactions on the account, each with its `transactionReference`. (Not re-verified in this
  pass — flagged below.)

Then confirm it server-side via section 3 (`GET /api/v2/transactions/{transactionReference}`,
url-encoding the `|` characters).

### The transaction-completed webhook (verbatim from Confluence "Webhook Events and Request Structure")

The full POST body is `{ "eventType": ..., "eventData": { ... } }`. For a successful reserved
account inflow:

- **`eventType`:** `"SUCCESSFUL_TRANSACTION"`

```json
{
  "eventType": "SUCCESSFUL_TRANSACTION",
  "eventData": {
    "product": {
      "reference": "1636106097661",
      "type": "RESERVED_ACCOUNT"
    },
    "transactionReference": "MNFY|04|20211117112842|000170",
    "paymentReference": "MNFY|04|20211117112842|000170",
    "paidOn": "2021-11-17 11:28:42.615",
    "paymentDescription": "Adm",
    "metaData": {},
    "paymentSourceInformation": [
      {
        "bankCode": "",
        "amountPaid": 3000,
        "accountName": "Monnify Limited",
        "sessionId": "e6cV1smlpkwG38Cg6d5F9B2PRnIq5FqA",
        "accountNumber": "0065432190"
      }
    ],
    "destinationAccountInformation": {
      "bankCode": "232",
      "bankName": "Sterling bank",
      "accountNumber": "6000140770"
    },
    "amountPaid": 3000,
    "totalPayable": 3000,
    "cardDetails": {},
    "paymentMethod": "ACCOUNT_TRANSFER",
    "currency": "NGN",
    "settlementAmount": "2990.00",
    "paymentStatus": "PAID",
    "customer": {
      "name": "John Doe",
      "email": "test@tester.com"
    }
  }
}
```

**Exact field paths the webhook handler should extract:**

- **`eventType`** — top-level; gate on `== "SUCCESSFUL_TRANSACTION"`.
- **transactionReference:** `eventData.transactionReference` (Monnify's id; contains `|`). Use this
  as the idempotency key and re-verify it server-side before crediting.
- **paymentReference:** `eventData.paymentReference`.
- **Destination (paid-into) account number:** `eventData.destinationAccountInformation.accountNumber`
  — compare against your reserved account (`4003115967`) to map the inflow.
  Destination bank: `eventData.destinationAccountInformation.bankName` / `.bankCode`.
- **Reserved account correlation key:** `eventData.product.reference` (with
  `eventData.product.type == "RESERVED_ACCOUNT"`). `product.reference` is your reserved account's
  `accountReference` — the reliable way to map the inflow to a subscription.
- **Amount:** `eventData.amountPaid` and `eventData.totalPayable`. NOTE: in this event-wrapped
  payload they are **JSON numbers** (`3000`), whereas the legacy flat webhook and the v2 status
  endpoint return **strings** (`"100.00"`). Parse defensively (accept number or string).
- **Status:** `eventData.paymentStatus` (`"PAID"`). **Payer** account (for reconciliation):
  `eventData.paymentSourceInformation[].accountNumber` / `.accountName`.

### Sandbox quirks / limitations

- Reserved-account inflows **can** be simulated in sandbox — via the simulator, not via any API
  credit endpoint and not via a dashboard button.
- **Amount + account number must match** exactly; wrong amount → `OVERPAID` / `PARTIALLY_PAID`.
- The sample destination in the doc (`232` / "Sterling bank" / `6000140770`) is Monnify's example,
  not your account — your webhook will show Wema bank and `4003115967`.
- `monnify-signature` header may be **absent in sandbox** (see the note in section 4 / Confidence
  below) — you may not be able to fully exercise signature verification against a simulated inflow.
- The reserved-account transactions **list endpoint path** above and the claim that no dashboard/API
  credit exists are called out under Confidence as not fully re-verified this pass.

---

## Confidence / unconfirmed

Confirmed verbatim from official docs:

- Auth endpoint, Basic scheme, and response shape (`accessToken` / `expiresIn: 3599`).
- Reserved account endpoint `POST /api/v2/bank-transfer/reserved-accounts` with full request and
  response JSON (response quoted verbatim from the Confluence "Reserve An Account V2" page).
- Get Transaction Status endpoint `GET /api/v2/transactions/{ref}` with verbatim response and the
  url-encode requirement.
- Webhook signature scheme: header `monnify-signature`, **HMAC-SHA512**, keyed with the client
  **secret key**, over the **raw request body**, hex output — confirmed across the Confluence
  "Calculating the Transaction Hash" page and the dev-portal/playground webhook pages, plus the PHP
  `hash_hmac('sha512', $request->getContent(), secret_key)` sample.
- Legacy flat webhook payload (transactionReference/paymentReference/amountPaid/paymentStatus/
  paidOn/transactionHash/paymentMethod) — quoted verbatim.
- Single transfer endpoint `POST /api/v2/disbursements/single` and the sample request/response.
- **Banking App Simulator** URL `https://websim.sdk.monnify.com/#/bankingapp` and that it simulates
  transfers to "any of the virtual accounts you created in the sandbox" (Confluence Integration
  Tools page). Simulator fields (Bank / Account number / exact Amount / Make Payment) and the "marks
  transaction as PAID and dispatches the webhook" behaviour — quoted from the dev-portal
  "Testing Pay with Transfer on Monnify Sandbox" blog.
- **`SUCCESSFUL_TRANSACTION` event-wrapped webhook payload** — now quoted **verbatim** from the
  Confluence "Webhook Events and Request Structure" page (supersedes the earlier "reconstructed"
  caveat for section 4b's structure): `eventType` + `eventData` with `product.reference`/`type`,
  `transactionReference`, `destinationAccountInformation.accountNumber`, numeric `amountPaid`, etc.

Could NOT fully confirm (verify against live sandbox before coding to these):

- **Reserved-account transactions list endpoint** `GET /api/v1/bank-transfer/reserved-accounts/
  transactions?accountReference=...` — path stated from memory of Monnify docs, not re-fetched
  verbatim this pass. Confirm exact path/params before relying on it; the webhook is the primary
  source of the `transactionReference` for a simulated inflow.
- **No dashboard "fund/simulate" button and no API credit endpoint for reserved accounts** — asserted
  from the absence of any such feature across the docs consulted; treat as "none found", not
  "proven not to exist". The simulator is the documented mechanism.

- **Current event-wrapped webhook payload** (`eventType` / `eventData` with `customer`,
  `destinationAccountInformation`, `product`): the dev-portal and playground pages render the JSON
  behind "Copy" buttons that did not extract as text, so the exact field-by-field structure shown
  in section 4b is **reconstructed** from field lists, not copied verbatim. Log and inspect a real
  sandbox webhook to pin down exact keys and nesting.
- **Sandbox may not send `monnify-signature`:** the playground webhook page indicated the
  `monnify-signature` header is "only included in production, not sandbox." **Flagged / not
  independently reconfirmed.** If true, you cannot end-to-end test signature verification in
  sandbox — build the verifier to fail-closed in prod but keep a config switch, and confirm header
  presence in the sandbox environment before relying on it.
- **Exact `paidOn` format string** shown as `dd/MM/yyyy hh:mm:ss a` inferred from the sample values;
  confirm with real data (sandbox vs webhook may differ from the v2 status endpoint).
- **Reserved-account `bvn`/`nin` requiredness:** docs say "either BVN or NIN or both" and tie it to
  transaction limits; whether your specific contract requires them is contract-dependent.
- **Single-transfer response shape** varies between doc generations (balance-style vs
  status/reference-style) — confirm against your contract.
- **v1 `verify_by_reference` vs v2 `/transactions/{ref}`:** both appear in current docs; the exact
  response field set for `verify_by_reference` (`settlementAmount`, `paymentSourceInformation`) was
  summarized from the dev-portal page, not quoted verbatim.

---

## Refund API (FR-11 — CONFIRMED 2026-07-20)

Source: `https://developers.monnify.com/docs/collections/manage-payments/refunds` and the dev-portal
API reference (`POST /api/v1/refunds/initiate-refund`), confirmed against a full request/response
sample.

**Endpoints** (both Bearer JWT from `/api/v1/auth/login`):
- Initiate: `POST {baseUrl}/api/v1/refunds/initiate-refund`
- Get status: `GET {baseUrl}/api/v1/refunds/{refundReference}` — same `responseBody` shape as initiate,
  used to **re-verify** a refund's status server-side rather than trusting the webhook (§8).

**Confirmed request fields** (initiate a refund):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `transactionReference` | string | yes | reference of the original payment being refunded |
| `refundReference` | string | yes | caller-supplied unique id for this refund — our idempotency key |
| `refundAmount` | number | yes | ₦100 minimum, up to the full transaction amount |
| `refundReason` | string | yes | internal reason, max 64 chars |
| `customerNote` | string | yes | credit-alert narration, max 16 chars |
| `destinationAccountNumber` | string | no | defaults to the originating account if omitted |
| `destinationAccountBankCode` | string | no | required only if a destination account is given |

**Confirmed response** — standard `{ requestSuccessful, responseMessage, responseCode, responseBody }`
envelope. `responseBody` fields: `refundReference`, `reference` (Monnify's `TRFD|...` id),
`transactionReference`, `refundReason`, `customerNote`, `refundAmount`, `refundType`
(`PARTIAL_REFUND` / `FULL_REFUND`), `refundStatus`, `refundStrategy` (e.g. `MERCHANT_WALLET`),
`comment`, `createdOn`, `destinationAccountName`, `destinationAccountNumber`, `destinationBankName`,
`currencyCode`. A 404 returns `requestSuccessful: false` with a `responseMessage`.

**Confirmed refund status values:** `PENDING`, `COMPLETED`, `FAILED`. (Note: the initiate response
may return `COMPLETED` with `comment: "Transaction refund is in progress."` — treat the
`SUCCESSFUL_REFUND` / `FAILED_REFUND` webhook as the authoritative terminal signal.)

**Confirmed webhook events:** `SUCCESSFUL_REFUND`, `FAILED_REFUND`.

**Our client** ([MonnifyClient.InitiateRefundAsync](../../src/OfrenCollect.Infrastructure/Monnify/MonnifyClient.cs))
sends the five required fields and omits the optional `destinationAccount*` (so refunds return to the
originating account), then maps `refundStatus` → `MonnifyRefundStatus`.

**FR-11.4 design:** the `SUCCESSFUL_REFUND` / `FAILED_REFUND` webhook is treated only as a *trigger* —
the drainer calls **Get status** to re-verify and acts on that, never on the webhook body (§8), exactly
as transaction reconciliation re-verifies with the transaction-status endpoint.

**Still to confirm:** the refund webhook payload shape (`eventType`/`eventData` nesting, which field
carries our `refundReference`) — inspect a real sandbox refund webhook. Only the `refundReference`
extraction depends on this; the status itself comes from the confirmed Get-status endpoint.

---

## Direct Debit / Mandate API (FR-9 — CONFIRMED 2026-07-21)

All Bearer JWT from `/api/v1/auth/login`. Standard `{ requestSuccessful, responseMessage, responseCode, responseBody }` envelope.

**Create mandate:** `POST /api/v1/direct-debit/mandate/create`
- Request (confirmed): `contractCode`, `mandateReference` (our idempotency key), `mandateAmount`,
  `autoRenew`, `customerCancellation`, `customerName`, `customerPhoneNumber`, `customerEmailAddress`,
  `customerAddress`, `customerAccountNumber`, `customerAccountBankCode`, `mandateDescription`,
  `mandateStartDate`/`mandateEndDate` (`YYYY-MM-DDTHH:MM:SS`), optional `redirectUrl`, `debitAmount` (nullable).
- Response body: `mandateReference`, `mandateCode` (e.g. `MTDD|...` — Monnify's id, returned AT creation),
  `mandateStatus` = `INITIATED`, `redirectUrl` (customer authorization link). Monnify emails an auth
  instruction; the customer authorizes via a token payment before the mandate becomes `ACTIVE`.

**Get mandate status:** `GET /api/v1/direct-debit/mandate/?mandateReferences=<ref>` → array with
`mandateCode`, `mandateReference`, `mandateStatus`, `authorizationLink`, etc.

**Debit mandate:** `POST /api/v1/direct-debit/mandate/debit`
- Request: `paymentReference` (our idempotency key for the debit), `mandateCode`, `debitAmount`,
  `narration`, `customerEmail`, optional `incomeSplitConfig`.
- Response: `transactionStatus` = `PENDING`, **`transactionReference` (`MNFY|...`)**, `paymentReference`.
  The `transactionReference` reconciles via the existing verify/transaction path.

**Get debit status:** `GET /api/v1/direct-debit/mandate/debit-status?paymentReference=<ref>` →
`transactionStatus` (`PAID`/...), `transactionReference`, `paymentReference`.

**Cancel mandate:** `PATCH /api/v1/direct-debit/mandate/cancel-mandate/{mandateCode}` →
`mandateStatus`.

**Mandate statuses:** `INITIATED`/`PENDING`, `ACTIVE`, `FAILED`, `CANCELLED`, `EXPIRED`.

**Still to confirm:** the **Mandate Status Change** webhook payload shape (which field carries our
`mandateReference` and the new status) — inspect a real sandbox webhook. Until then, mandate
activation/cancellation can be reconciled by polling Get-mandate-status. Also confirm the sandbox
supports the full direct-debit authorization flow end-to-end.
