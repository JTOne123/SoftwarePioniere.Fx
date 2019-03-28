﻿using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace SoftwarePioniere.Clients
{
    public static class HttpClientExtensions
    {

        public static async Task<T> GetAsAsync<T>(this HttpClient client, string uri)
        {
            var response = await client.GetAsync(uri);

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new AuthenticationException(response.ReasonPhrase);
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return default(T);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(response.ReasonPhrase);
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsAsync<T>();

        }
    }
}