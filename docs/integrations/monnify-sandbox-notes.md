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
  "contractCode": "8389328412",
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
    "contractCode": "222001311614",
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

Could NOT fully confirm (verify against live sandbox before coding to these):

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
