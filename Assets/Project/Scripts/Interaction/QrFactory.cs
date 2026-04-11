using System;
using System.Collections.Generic;
using Project.Scripts.Interaction.Interactive_Objects;
using UnityEngine;

using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Project.Scripts.Interaction
{
    public sealed class QrFactory
    {
        private readonly IObjectResolver objectResolver;
        private readonly IQrMarkerRegistry markerRegistry;

        private readonly Dictionary<string, GameObject> spawnedByMarkerId =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        public QrFactory(
            IObjectResolver objectResolver,
            IQrMarkerRegistry markerRegistry)
        {
            this.objectResolver = objectResolver;
            this.markerRegistry = markerRegistry;
        }

        public bool TryCreateMarker(string markerId, in Pose pose, out GameObject instance)
        {
            instance = null;
            if (string.IsNullOrWhiteSpace(markerId))
            {
                return false;
            }

            if (!markerRegistry.TryGet(markerId, out var definition) || definition == null || definition.Prefab == null)
            {
                return false;
            }

            var rotation = pose.rotation * Quaternion.Euler(definition.RotationOffset);
            var position = pose.position + pose.rotation * definition.PositionOffset;
            instance = objectResolver.Instantiate(definition.Prefab, position, rotation);

            spawnedByMarkerId[markerId] = instance;
            return instance != null;
        }

        public bool TryGetSpawnedMarker(string markerId, out GameObject instance)
        {
            instance = null;
            if (string.IsNullOrWhiteSpace(markerId))
            {
                return false;
            }

            if (!spawnedByMarkerId.TryGetValue(markerId, out var spawned) || spawned == null)
            {
                return false;
            }

            instance = spawned;
            return true;
        }

        public void CreateButton(
            GameObject buttonPrefab,
            string markerId,
            in Pose pose,
            out QrButton button)
        {
            var buttonObject = Object.Instantiate(buttonPrefab, pose.position, pose.rotation);
            buttonObject.TryGetComponent<QrButton>(out button);
            button.Initialize(markerId, HandleButtonInteract);
        }

        private void HandleButtonInteract(string markerId, Pose pose)
        {
            TryCreateMarker(markerId, pose, out _);
        }
    }
}
