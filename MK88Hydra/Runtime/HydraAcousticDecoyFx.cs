using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Small underwater bubble puffs at decoy points.</summary>
    internal sealed class HydraAcousticDecoyFx : MonoBehaviour
    {
        private ParticleSystem? _ps;
        private float _nextEmit;

        internal void Init(Transform parent)
        {
            if (_ps != null)
                return;

            var go = new GameObject("HydraDecoyBubbles");
            go.transform.SetParent(parent, false);
            _ps = go.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.loop = false;
            main.startLifetime = TorpedoConstants.DecoyBubbleLifetimeS;
            main.startSpeed = TorpedoConstants.DecoyBubbleSpeedMps;
            main.startSize = TorpedoConstants.DecoyBubbleSizeM;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.35f);

            var emission = _ps.emission;
            emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = false;
        }

        internal void EmitAt(Vector3 worldPos)
        {
            if (_ps == null)
                return;
            var emit = new ParticleSystem.EmitParams
            {
                position = worldPos,
                velocity = Vector3.up * TorpedoConstants.DecoyBubbleSpeedMps
            };
            _ps.Emit(emit, 1);
        }

        private void Update()
        {
            if (_ps == null || Time.time < _nextEmit)
                return;
            _nextEmit = Time.time + TorpedoConstants.DecoyBubbleIntervalS;
        }
    }
}
