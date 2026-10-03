# Commercial architecture and multi-simulator compatibility

This document records the separation between editing, billing, licensing, and simulator support.

## 1. Current state

During Alpha development and validation, Map Studio runs in `DevelopmentPreview`:

- billing is not enforced;
- all features remain available;
- no fake license is generated;
- no license server is mocked;
- development builds can be tested without a commercial account.

When the product enters commercial distribution, `EnforcementEnabled` can be enabled and the same feature gates can require real entitlements.

## 2. Stripe: billing, not editor authority

Stripe is the planned initial billing/subscription provider.

The desktop application must NOT:

- contain a Stripe secret key;
- create subscriptions directly with privileged credentials;
- validate webhooks;
- independently decide whether a subscription is active;
- trust only locally cached subscription state.

Planned flow:

1. Desktop authenticates the user with the Map Studio commercial service.
2. Desktop asks the backend for a Checkout URL.
3. Backend creates a Stripe Checkout Session in subscription mode.
4. User completes payment on the hosted page.
5. Stripe sends webhooks to the backend.
6. Backend converts subscription state into Map Studio entitlements.
7. Desktop requests a signed/verifiable entitlement state.
8. The backend creates a Customer Portal session when the user wants to manage billing.

The entitlement/license backend itself will be designed later.

## 3. Entitlements

Core already defines keys independently from Stripe prices and products:

- `editor.core`;
- `creation.building-studio`;
- `creation.procedural-roads`;
- `creation.ai-assistance`;
- `simulator.omsi2`;
- `simulator.proton-bus`;
- `simulator.lotus`.

This leaves room for later decisions such as:

- one all-inclusive plan;
- tiered plans;
- simulator add-ons;
- AI add-on;
- perpetual licenses for specific versions;
- monthly/yearly subscriptions;
- combinations of these models.

No pricing decision is hard-coded today.

## 4. Licensing and billing are separate layers

Stripe reports billing state. The Map Studio commercial backend will translate that state into product access.

A future entitlement layer may cover:

- user account;
- active subscription;
- grace period;
- end-of-period cancellation;
- failed/late payments;
- device/session policy if adopted;
- limited offline cache if adopted;
- revocation;
- signed entitlement responses;
- plan migration.

These rules must remain outside simulator parsers and writers.

## 5. Multi-simulator architecture

The project now has a formal adapter boundary under `MapStudio.Core.Simulators`.

Today:

- OMSI 2 has a registered adapter;
- Proton Bus is a known ID but is NOT advertised as supported;
- LOTUS is a known ID but is NOT advertised as supported;
- future simulators can be added through real adapters.

An adapter declares capabilities such as:

- map discovery;
- map editing;
- terrain;
- roads;
- scenery;
- asset library;
- traffic;
- public transport;
- signals/rail;
- procedural generation;
- Building Studio;
- validation.

A simulator must only be marked as supported when its adapter actually implements the required workflows.

## 6. Responsibility boundaries

Target direction:

```text
Map Studio WinUI
    |
    +-- Commercial core / feature gates
    |
    +-- Procedural creation core
    |
    +-- Building Studio / AI
    |
    +-- Simulator Registry
           |
           +-- OMSI 2 adapter
           +-- Proton Bus adapter (future)
           +-- LOTUS adapter (future)
           +-- other adapters
```

Each adapter translates the Map Studio domain into the actual formats of its simulator.

## 7. AI rule

AI remains provider-neutral.

Map Studio subscription and AI provider are separate concepts:

- a customer may subscribe to Map Studio;
- a plan may grant the AI entitlement;
- the customer may connect the AI provider they prefer;
- AI-provider credentials are not Stripe credentials;
- billing must not force the customer to use one specific AI vendor.

## 8. Next commercial steps

After the editor architecture stabilizes:

1. define products/plans;
2. create the commercial backend;
3. configure Stripe sandbox;
4. create subscription Checkout;
5. create Customer Portal integration;
6. receive and verify webhooks;
7. persist accounts/subscriptions/entitlements;
8. define offline policy;
9. sign entitlement responses;
10. enable `EnforcementEnabled` only after full validation.

Until then, development mode stays explicitly unlocked.
