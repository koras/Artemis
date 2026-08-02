using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Offers;
using UnityEngine;

namespace _Project.Scripts.Systems.Offers.Runtime
{
    /// <summary>
    /// Encapsulates customer reputation rules and portrait selection by reputation bands.
    /// </summary>
    public sealed class OfferReputationService
    {
        private const int DEFAULT_CUSTOMER_REPUTATION = 50;
        private const int MIN_REPUTATION = 0;
        private const int MAX_REPUTATION = 100;

        private readonly OfferSystemContext _context;

        /// <summary>
        /// Creates reputation service over shared runtime context.
        /// </summary>
        public OfferReputationService(OfferSystemContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns customer reputation or default baseline when no value exists.
        /// </summary>
        public int GetCustomerReputation(OfferCustomerDefinition customer)
        {
            if (customer == null)
            {
                return DEFAULT_CUSTOMER_REPUTATION;
            }

            if (_context.ReputationByCustomer.TryGetValue(customer, out int reputation))
            {
                return reputation;
            }

            return DEFAULT_CUSTOMER_REPUTATION;
        }

        /// <summary>
        /// Returns reward multiplier where each 10 reputation above baseline adds 10%.
        /// </summary>
        public float GetRewardMultiplier(OfferCustomerDefinition customer)
        {
            int reputation = GetCustomerReputation(customer);
            int delta = Mathf.Max(0, reputation - DEFAULT_CUSTOMER_REPUTATION);
            int steps = delta / 10;
            return 1f + (steps * 0.1f);
        }

        /// <summary>
        /// Applies delta and clamps resulting reputation to allowed range.
        /// </summary>
        public void ApplyReputation(OfferCustomerDefinition customer, int delta)
        {
            if (customer == null || delta == 0)
            {
                return;
            }

            int current = GetCustomerReputation(customer);
            int next = Mathf.Clamp(current + delta, MIN_REPUTATION, MAX_REPUTATION);
            _context.ReputationByCustomer[customer] = next;
        }

        /// <summary>
        /// Selects portrait variant according to current customer reputation.
        /// </summary>
        public Sprite GetCustomerPortrait(OfferCustomerDefinition customer)
        {
            if (customer == null)
            {
                return null;
            }

            int reputation = GetCustomerReputation(customer);
            if (reputation >= 70)
            {
                return customer.KindPortrait;
            }

            if (reputation >= 40)
            {
                return customer.NeutralPortrait;
            }

            if (reputation >= 20)
            {
                return customer.AngryPortrait;
            }

            return customer.VeryAngryPortrait;
        }

        /// <summary>
        /// Builds deterministic lookup key map for customer definitions from offer catalog.
        /// </summary>
        public Dictionary<string, OfferCustomerDefinition> BuildCustomerByKey()
        {
            var result = new Dictionary<string, OfferCustomerDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < _context.Catalog.Count; i++)
            {
                OfferCustomerDefinition customer = _context.Catalog[i].Customer;
                if (customer == null)
                {
                    continue;
                }

                string customerKey = GetCustomerKey(customer);
                if (string.IsNullOrWhiteSpace(customerKey) || result.ContainsKey(customerKey))
                {
                    continue;
                }

                result.Add(customerKey, customer);
            }

            return result;
        }

        /// <summary>
        /// Builds stable customer key from the technical customer localization ID.
        /// </summary>
        public static string GetCustomerKey(OfferCustomerDefinition customer)
        {
            return customer == null ? string.Empty : customer.LocalizationId;
        }
    }
}