using System;

namespace OAuth2Test
{
    public enum Mode
    {
        uaa,
        keycloak
    }

    public abstract class OAuth2OptionsBase
    {
        protected readonly Mode _mode;

        public OAuth2OptionsBase(Mode mode)
        {
            _mode = mode;
        }

        public string Name
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => "uaa",
                    Mode.keycloak => "keycloak",
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        public string Scope
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => string.Empty,
                    Mode.keycloak => "rabbitmq:configure:*/* rabbitmq:read:*/* rabbitmq:write:*/*",
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        public string TokenEndpoint // => _mode switch
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => "http://localhost:8080/oauth/token",
                    Mode.keycloak => "http://localhost:8080/realms/test/protocol/openid-connect/token",
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        public abstract string ClientId { get; }
        public abstract string ClientSecret { get; }

        public static int TokenExpiresInSeconds => 60;
    }

    public class OAuth2ProducerOptions : OAuth2OptionsBase
    {
        public OAuth2ProducerOptions(Mode mode) : base(mode)
        {
        }

        public override string ClientId => "producer";

        public override string ClientSecret
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => "producer_secret",
                    Mode.keycloak => "kbOFBXI9tANgKUq8vXHLhT6YhbivgXxn",
                    _ => throw new InvalidOperationException(),
                };
            }
        }
    }

    public class OAuth2HttpApiOptions : OAuth2OptionsBase
    {
        public OAuth2HttpApiOptions(Mode mode) : base(mode)
        {
        }

        public override string ClientId
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => "mgt_api_client",
                    Mode.keycloak => "mgt_api_client",
                    _ => throw new InvalidOperationException(),
                };
            }
        }

        public override string ClientSecret
        {
            get
            {
                return _mode switch
                {
                    Mode.uaa => "mgt_api_client",
                    Mode.keycloak => "LWOuYqJ8gjKg3D2U8CJZDuID3KiRZVDa",
                    _ => throw new InvalidOperationException(),
                };
            }
        }
    }
}
