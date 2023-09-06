# OAuth2 Test Application

Application to test and demonstrate OAuth2 authorization and token refresh
in a .Net Console application.

There are Authorization servers like UAA which do not return a `refresh_token`
when using *Client credentials* flow to request a token. This means that the
.Net client must request a new token before it expires. Whereas there are other
Authorization servers like Keycloak which does return one. This means that the
.Net client refreshes the token before it expires.

To test against UAA follow these steps:
1. Start UAA -> `./start-uaa.sh`
2. Start RabbitMQ with MODE=uaa -> `MODE=uaa ./start-rabbit.sh`
3. Start Test with MODE=uaa -> `MODE=uaa ./start-test.sh`

To test against Keycloak follow these steps:
1. Start Keycloak -> `./start-keycloak.sh`
2. Start RabbitMQ with MODE=keycloak -> `MODE=keycloak ./start-rabbit.sh`
3. Start Test with MODE=keycloak -> `MODE=keycloak ./start-test.sh`

## Authorization Servers

Both Authorization servers have the following relevant configuration:
- An oauth client called `producer`
- A RSA signing key
- Access Token lifetime of 1 minute

## Test Application

The Test Application declares an exchange, a queue, binds the queue to the exchange,
and sends 4 messages and waits for them and in between each message it waits until
the token has expired to force a token refresh.
