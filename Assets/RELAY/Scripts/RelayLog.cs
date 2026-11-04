using System;
using UnityEngine;

namespace Relay
{
    /// <summary>Every RELAY event goes through here, so the Console and the data files always agree.</summary>
    public static class RelayLog
    {
        public static event Action<string, string> Logged;

        public static void Event(string code, string detail = "")
        {
            Debug.Log($"[RELAY {Time.time:F3}] {code} {detail}");
            Logged?.Invoke(code, detail);
        }
    }
}
