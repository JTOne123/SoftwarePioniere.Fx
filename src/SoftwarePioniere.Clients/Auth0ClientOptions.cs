﻿// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace SoftwarePioniere.Clients
{
    public class Auth0ClientOptions
    {
        public string Audience { get; set; } = "https://api.softwarepioniere.de";

        public string ClientSecret { get; set; }

        public string ClientId { get; set; }

        public string TenantId { get; set; }

        public string Connection { get; set; } = "Username-Password-Authentication";
    }
}