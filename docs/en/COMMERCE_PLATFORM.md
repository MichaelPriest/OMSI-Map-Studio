# OMSI Map Studio commerce platform

## Goal

The Public Alpha platform is split into four areas:

1. public marketing and purchase website;
2. authenticated subscriber portal;
3. administration portal;
4. licensing and update APIs consumed by the WinUI application.

## Initial stack

- Next.js for the website, portals and API endpoints;
- Supabase Auth + PostgreSQL for accounts, subscriptions, licenses, devices, entitlements and releases;
- Stripe Checkout + webhooks for billing;
- Ed25519-signed entitlement tokens for offline verification by the application.

Stripe secrets, the Supabase Service Role key and the signing private key remain server-side only.

## License

A successful subscription enables `editor.core` and creates a serial in the format:

`OMS-MS-A5-XXXX-XXXX-XXXX`

The activation endpoint registers a device, enforces the device limit and returns a signed token. The desktop application embeds only the Ed25519 public key.

The initial policy allows up to 2 computers per license.

## Updates

Admin publishes release manifests for the `alpha` and `stable` channels.

The desktop client calls:

`GET /api/updates/latest?channel=alpha`

The response includes version, release notes, hash, minimum version and mandatory-update state.

Downloads are routed through:

`GET /api/download/latest?channel=alpha`

and require the `editor.core` entitlement.

## First implementation state

The commerce branch now contains:

- public homepage;
- subscription page;
- registration/login;
- Stripe Checkout;
- subscription webhook;
- automatic serial creation;
- `editor.core` entitlement;
- device activation;
- Ed25519 offline token;
- subscriber portal;
- Admin portal;
- release publishing;
- update manifest;
- entitlement-gated downloads;
- PostgreSQL schema + RLS.

Still required for production:

- create a dedicated Map Studio Supabase project;
- configure Stripe product/price;
- register Stripe webhook;
- configure environment variables;
- generate Ed25519 key pair;
- add Stripe Customer Portal;
- allow subscribers to revoke devices;
- integrate licensing and update checker in WinUI;
- deploy the website to the official domain;
- review Public Alpha terms, privacy and refund policy.
