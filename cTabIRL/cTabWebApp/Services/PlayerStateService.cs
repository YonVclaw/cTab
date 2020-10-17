﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace cTabWebApp
{
    internal class PlayerStateService : IPlayerStateService
    {
        private static readonly List<PlayerState> players = new List<PlayerState>();
        private static int nextId = 1;

        public PlayerState GetStateByToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }
            lock (players)
            {
                return players.FirstOrDefault(p => p.Token == token);
            }
        }

        public KeyLoginResult GetTokenBySteamIdAndKey(string steamId, string key)
        {
            List<PlayerState> candidates;
            lock (players)
            {
                candidates = players.Where(p => p.SteamId == steamId).ToList();
            }
            if (candidates.Count == 0)
            {
                return new KeyLoginResult(KeyLoginState.UnknownPlayer, null);
            }
            var state = candidates.FirstOrDefault(p => p.HashedKey == HashKeyForHost(key, p.KeyHostname));
            if (state == null)
            {
                return new KeyLoginResult(KeyLoginState.BadKey, null);
            }
            return new KeyLoginResult(KeyLoginState.Ok, state.Token);
        }

        public PlayerState GetOrCreateStateBySteamIdAndKey(string steamId, string hashedKey, string keyHostname)
        {
            PlayerState state;
            lock (players)
            {
                state = players.FirstOrDefault(p => p.SteamId == steamId && p.HashedKey == hashedKey && p.KeyHostname == keyHostname);
            }
            if (state == null)
            {
                state = new PlayerState()
                {
                    Id = Interlocked.Increment(ref nextId),
                    HashedKey = hashedKey,
                    SteamId = steamId,
                    KeyHostname = keyHostname,
                    Token = GenerateToken(),
                    LastActivityUtc = DateTime.UtcNow
                };
                lock (players)
                {
                    players.RemoveAll(p => p.ActiveConnections == 0 && p.LastActivityUtc < DateTime.UtcNow.AddDays(-1));
                    players.Add(state);
                }
            }
            return state;
        }

        private static string HashKeyForHost(string key, string hostname)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            return Convert.ToBase64String(KeyDerivation.Pbkdf2(
                            password: key,
                            salt: Encoding.UTF8.GetBytes(hostname),
                            prf: KeyDerivationPrf.HMACSHA256,
                            iterationCount: 10000,
                            numBytesRequested: 256 / 8));
        }

        private static string GenerateToken()
        {
            var random = new byte[32];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(random);
            return Convert.ToBase64String(random).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
