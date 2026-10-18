using System;
using Bomome.EditorTools.Build;
using UnityEngine;

namespace Bomome.EditorTools
{
    [CreateAssetMenu(menuName = "Bomome/Editor Tools/Firebase Distribution Settings")]
    public sealed class FirebaseDistributionToolSettings : ScriptableObject
    {
        public FirebaseDistributionSettings.Destination Development = new FirebaseDistributionSettings.Destination();
        public FirebaseDistributionSettings.Destination Stage = new FirebaseDistributionSettings.Destination();
        public FirebaseDistributionSettings.Destination Production = new FirebaseDistributionSettings.Destination();

        public FirebaseDistributionSettings.Destination GetDestination(int environment)
        {
            switch (environment)
            {
                case 0: return Development;
                case 1: return Stage;
                case 2: return Production;
                default: throw new ArgumentOutOfRangeException(nameof(environment));
            }
        }
    }
}
