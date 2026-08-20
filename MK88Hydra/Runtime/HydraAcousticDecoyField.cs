using System.Collections.Generic;
using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Passive hydroacoustic traps — each rolls 7% to seduce inbound torpedoes.</summary>
    internal sealed class HydraAcousticDecoyField : MonoBehaviour
    {
        private static readonly List<HydraAcousticDecoyField> Active = new List<HydraAcousticDecoyField>(16);

        private Ship? _ship;
        private Vector3[] _localPoints = System.Array.Empty<Vector3>();
        private HydraAcousticDecoyFx? _fx;
        private float _nextBubble;
        private int _bubbleIdx;

        internal static void AttachIfNeeded(Ship ship)
        {
            if (ship == null || ship.GetComponent<HydraAcousticDecoyField>() != null)
                return;

            int count = HydraAcousticDecoyRegistry.ResolveTrapCount(ship.definition);
            if (count <= 0)
                return;

            var field = ship.gameObject.AddComponent<HydraAcousticDecoyField>();
            field.Init(ship, count);
        }

        internal static bool IsInAnyField(Vector3 worldPos)
        {
            for (int f = 0; f < Active.Count; f++)
            {
                HydraAcousticDecoyField field = Active[f];
                if (field._ship == null || field._ship.disabled)
                    continue;
                if (field.IsInField(worldPos))
                    return true;
            }
            return false;
        }

        internal static bool TrySeduce(Vector3 torpedoPos, out Vector3 decoyWorldPos)
        {
            decoyWorldPos = default;
            float bestSq = float.MaxValue;
            bool found = false;

            for (int f = 0; f < Active.Count; f++)
            {
                HydraAcousticDecoyField field = Active[f];
                if (field._ship == null || field._ship.disabled)
                    continue;
                if (!field.IsInField(torpedoPos))
                    continue;

                if (!field.TryRollTrap(torpedoPos, out Vector3 trapPos))
                    continue;

                float sq = (trapPos - torpedoPos).sqrMagnitude;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                decoyWorldPos = trapPos;
                found = true;
            }

            return found;
        }

        private void Init(Ship ship, int trapCount)
        {
            _ship = ship;
            _localPoints = BuildTrapPoints(trapCount, ship);
            _fx = gameObject.AddComponent<HydraAcousticDecoyFx>();
            _fx.Init(transform);
            Active.Add(this);
            HydraPlugin.ModLog?.LogInfo(
                $"Hydra decoys: {trapCount} traps on '{ship.definition?.unitName ?? ship.name}'");
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (_ship == null || _ship.disabled || _localPoints.Length == 0 || _fx == null)
                return;
            if (Time.time < _nextBubble)
                return;

            _nextBubble = Time.time + TorpedoConstants.DecoyBubbleIntervalS;
            _bubbleIdx = (_bubbleIdx + 1) % _localPoints.Length;
            Vector3 p = _ship.transform.TransformPoint(_localPoints[_bubbleIdx]);
            p.y = Datum.LocalSeaY - TorpedoConstants.DecoyBubbleDepthM;
            _fx.EmitAt(p);
        }

        private bool IsInField(Vector3 worldPos)
        {
            if (_ship == null)
                return false;
            Vector3 center = _ship.transform.position;
            center.y = Datum.LocalSeaY - TorpedoConstants.SwimDepthM;
            float dx = worldPos.x - center.x;
            float dz = worldPos.z - center.z;
            float r = TorpedoConstants.DecoyFieldRadiusM;
            return dx * dx + dz * dz <= r * r;
        }

        private bool TryRollTrap(Vector3 torpedoPos, out Vector3 trapWorldPos)
        {
            trapWorldPos = default;
            if (_ship == null || _localPoints.Length == 0)
                return false;

            float bestSq = float.MaxValue;
            bool hit = false;
            for (int i = 0; i < _localPoints.Length; i++)
            {
                if (Random.value >= TorpedoConstants.DecoyTrapRedirectChance)
                    continue;

                Vector3 p = _ship.transform.TransformPoint(_localPoints[i]);
                p.y = Datum.LocalSeaY - TorpedoConstants.SwimDepthM;
                float sq = (p - torpedoPos).sqrMagnitude;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                trapWorldPos = p;
                hit = true;
            }

            return hit;
        }

        private static Vector3[] BuildTrapPoints(int count, Ship ship)
        {
            count = Mathf.Clamp(count, 1, 256);
            var pts = new Vector3[count];
            float len = ship.definition != null ? Mathf.Max(ship.definition.length, 80f) : 120f;
            float beam = ship.definition != null ? Mathf.Max(ship.definition.width, 12f) : 20f;
            float halfLen = len * 0.45f;
            float halfBeam = beam * 0.35f;
            float depth = TorpedoConstants.SwimDepthM;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float side = (i & 1) == 0 ? 1f : -1f;
                float wobble = Mathf.Sin(i * 0.73f) * halfBeam * 0.35f;
                pts[i] = new Vector3(
                    Mathf.Lerp(-halfLen, halfLen, t),
                    -depth,
                    side * halfBeam * 0.55f + wobble);
            }

            return pts;
        }
    }
}
