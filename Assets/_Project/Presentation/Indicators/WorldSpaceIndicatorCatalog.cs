using System;
using System.Collections.Generic;
using UnityEngine;

namespace VrGame.Presentation.Indicators
{
    [Serializable]
    public sealed class IndicatorVariantDefinition
    {
        [SerializeField]
        private IndicatorVariant variant;

        [SerializeField]
        private WorldSpaceIndicatorView prefab;

        [SerializeField]
        private Vector3 localOffset = new Vector3(0f, 0.35f, 0f);

        public IndicatorVariant Variant => variant;

        public WorldSpaceIndicatorView Prefab => prefab;

        public Vector3 LocalOffset => localOffset;
    }

    [CreateAssetMenu(menuName = "VR Game/Indicators/Catalog", fileName = "WorldSpaceIndicatorCatalog")]
    public sealed class WorldSpaceIndicatorCatalog : ScriptableObject
    {
        [SerializeField]
        private IndicatorVariantDefinition[] definitions = Array.Empty<IndicatorVariantDefinition>();

        public IndicatorVariantDefinition GetRequired(IndicatorVariant variant)
        {
            ValidateOrThrow();
            foreach (var definition in definitions)
            {
                if (definition.Variant == variant)
                    return definition;
            }

            throw new InvalidOperationException($"Indicator variant '{variant}' отсутствует в catalog.");
        }

        public void ValidateOrThrow()
        {
            if (definitions == null)
                throw new InvalidOperationException("Коллекция indicator definitions отсутствует.");

            var seen = new HashSet<IndicatorVariant>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new InvalidOperationException("Catalog содержит пустую indicator definition.");
                if (!Enum.IsDefined(typeof(IndicatorVariant), definition.Variant))
                    throw new InvalidOperationException("Catalog содержит неизвестный indicator variant.");
                if (definition.Prefab == null)
                    throw new InvalidOperationException($"У indicator variant '{definition.Variant}' отсутствует prefab.");
                if (!IsFinite(definition.LocalOffset))
                    throw new InvalidOperationException($"У indicator variant '{definition.Variant}' некорректный offset.");
                if (!seen.Add(definition.Variant))
                    throw new InvalidOperationException($"Indicator variant '{definition.Variant}' задан больше одного раза.");
            }

            foreach (IndicatorVariant variant in Enum.GetValues(typeof(IndicatorVariant)))
            {
                if (!seen.Contains(variant))
                    throw new InvalidOperationException($"Indicator variant '{variant}' отсутствует в catalog.");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
