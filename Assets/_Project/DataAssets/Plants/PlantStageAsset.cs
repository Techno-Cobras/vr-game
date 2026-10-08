using System;
using UnityEngine;

namespace VrGame.DataAssets.Plants
{
    [Serializable]
    public sealed class PlantStageAsset
    {
        [SerializeField, Range(0f, 1f)]
        private float startsAtNormalized;

        [SerializeField]
        private PlantRepresentationAsset representation;

        public float StartsAtNormalized => startsAtNormalized;
        public PlantRepresentationAsset Representation => representation;
    }
}
