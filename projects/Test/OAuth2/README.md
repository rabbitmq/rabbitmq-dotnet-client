# OAuth2 Test Suite

This is a test suite to test and demonstrate OAuth2 authorization and token
refresh.

There are Authorization servers like UAA which do not return a `refresh_token`
when using *Client credentials* flow to request a token. This means that the
.Net client must request a new token before it expires. Whereas there are other
Authorization servers like Keycloak which does return one. This means that the
.Net client refreshes the token before it expires.

### To test against UAA follow these steps:
1. Start UAA and RabbitMQ -> `./.ci/oauth2/setup.sh uaa`
2. Start test -> ` ./.ci/oauth2/test.sh uaa`

### To test against Keycloak follow these steps:
1. Start Keycloak and RabbitMQ -> `./.ci/oauth2/setup.sh keycloak`
2. Start test -> ` ./.ci/oauth2/test.sh keycloak`

### Teardown
`./.ci/oauth2/teardown.sh`

## Authorization Servers

Both Authorization servers have the following relevant configuration:
- An oauth client called `producer`
- A RSA signing key
- Access Token lifetime of 1 minute

## Integration Test

The Integration test declares an exchange, a queue, binds the queue to the
exchange, and sends 4 messages and waits for them and in between each message
it waits until the token has expired to force a token refresh.
