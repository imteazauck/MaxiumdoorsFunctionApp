# MaxiumDoorsFunctionApp

Azure Functions backend for Maxium Doors.

## Reseller admin APIs added

These routes require the same admin bearer token used by the existing backoffice order endpoints.

- `GET /api/backoffice/resellers`
- `POST /api/backoffice/resellers`
- `GET /api/backoffice/resellers/{resellerId}`
- `PUT /api/backoffice/resellers/{resellerId}`
- `DELETE /api/backoffice/resellers/{resellerId}`
- `GET /api/backoffice/resellers/{resellerId}/pricing`
- `PUT /api/backoffice/resellers/{resellerId}/pricing/{itemId}`
- `GET /api/backoffice/resellers/{resellerId}/credentials`
- `POST /api/backoffice/resellers/{resellerId}/credentials`

## Cosmos DB containers

Add these settings:

- `CosmosDb:DatabaseId`
- `CosmosDb:ContainerId` for orders
- `CosmosDb:ResellersContainerId`
- `CosmosDb:PricingContainerId`

Recommended partition key for the reseller-related containers:

- `Resellers`: `/resellerId`
- `ResellerPricing`: `/resellerId`

## Default pricing template cloning

When a reseller is created, the API clones the bundled default pricing template from `Data/defaultPricingTemplate.json` into the reseller pricing container so each reseller gets an independent pricing model.

## Password handling status

Credential endpoints are included, but password hashing is still deferred. The current code keeps password handling as a temporary development placeholder and should be hardened before production use.
