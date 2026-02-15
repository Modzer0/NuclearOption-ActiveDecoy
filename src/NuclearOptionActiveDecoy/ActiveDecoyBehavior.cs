using UnityEngine;

namespace NuclearOptionActiveDecoy
{
    /// <summary>
    /// The physical decoy object that flies through the air after being ejected.
    /// It mimics the launching aircraft's radar return at a stronger level,
    /// causing radar-guided missiles to retarget onto it.
    /// 
    /// Implementation approach: The decoy increases the aircraft's apparent RCS
    /// by adding jamming intensity (negative jamming = RCS boost is not how the
    /// game works), so instead we use the onJam event system to broadcast a
    /// stronger false return. The decoy registers itself as an IRadarReturn
    /// target that missiles can lock onto.
    /// 
    /// Since we can't easily make the decoy a full Unit/IRadarReturn (those are
    /// deeply integrated with networking), we instead use Harmony patches on the
    /// missile seekers to check for nearby active decoys and retarget onto them.
    /// </summary>
    public class ActiveDecoyBehavior : MonoBehaviour
    {
        public Aircraft sourceAircraft;
        public float decoyRCS;
        public float lifetime;
        public Vector3 velocity;
        public bool isActive;

        private float spawnTime;
        private float drag;
        private Vector3 gravityVector = new Vector3(0f, -9.81f, 0f);

        // Track all active decoys globally for seeker patches to query
        public static readonly System.Collections.Generic.List<ActiveDecoyBehavior> ActiveDecoys
            = new System.Collections.Generic.List<ActiveDecoyBehavior>();

        public void Initialize(Aircraft aircraft, Vector3 launchVelocity, float rcs, float life, float dragCoef)
        {
            this.sourceAircraft = aircraft;
            this.velocity = launchVelocity;
            this.decoyRCS = rcs;
            this.lifetime = life;
            this.drag = dragCoef;
            this.spawnTime = Time.timeSinceLevelLoad;
            this.isActive = true;

            ActiveDecoys.Add(this);

            Plugin.Log.LogDebug($"Active decoy launched: RCS={rcs:F4}, lifetime={life}s");
        }

        private void Update()
        {
            if (!isActive) return;

            // Physics movement
            this.transform.position += this.velocity * Time.deltaTime;
            this.velocity += this.gravityVector * Time.deltaTime;
            this.velocity -= this.velocity * this.drag * Time.deltaTime;

            // Ground collision
            if (this.transform.position.y < 0f)
            {
                this.velocity = Vector3.zero;
                this.transform.position = new Vector3(
                    this.transform.position.x, 0.1f, this.transform.position.z);
            }

            // Lifetime check
            if (Time.timeSinceLevelLoad - this.spawnTime > this.lifetime)
            {
                Deactivate();
            }
        }

        private void Deactivate()
        {
            this.isActive = false;
            ActiveDecoys.Remove(this);
            Destroy(this.gameObject, 1f);
        }

        private void OnDestroy()
        {
            ActiveDecoys.Remove(this);
        }

        /// <summary>
        /// Calculates the radar return this decoy would present to a seeker at the given position.
        /// The effectiveness parameter scales the return (1.0 = full, 0.25 = degraded).
        /// </summary>
        public float GetDecoyRadarReturn(Vector3 seekerPosition, RadarParams radarParams, float effectiveness = 1f)
        {
            if (!isActive) return 0f;

            float dist = Vector3.Distance(seekerPosition, this.transform.position);
            if (dist < 1f) dist = 1f;

            // Simplified radar return: maxRange/dist * RCS^0.25
            float signal = radarParams.maxRange / dist * Mathf.Pow(this.decoyRCS, 0.25f);
            signal = Mathf.Min(signal, radarParams.maxSignal);

            return signal * effectiveness;
        }

        /// <summary>
        /// Calculates how effective the decoy is based on the aircraft's behavior.
        /// Two independent factors multiply together:
        ///   - Radar active: 0.5x (radiating makes the decoy less convincing)
        ///   - Heading toward missile: 0.5x (decoy is behind you, less effective)
        /// Both penalties together = 0.25x. Notching with radar off = 1.0x (full).
        /// Notching with radar on = 0.5x (still workable).
        /// </summary>
        public float GetEffectiveness(Vector3 seekerPosition)
        {
            if ((Object)sourceAircraft == null) return 0.25f;

            float effectiveness = 1f;
            float penaltyFactor = Mathf.Sqrt(
                Plugin.PenaltyMultiplier != null ? Plugin.PenaltyMultiplier.Value : 0.25f);
            // sqrt(0.25) = 0.5, so each independent penalty is 0.5x, both together = 0.25x

            // Penalty 1: Aircraft radar is active
            if (sourceAircraft.radar != null && sourceAircraft.radar.activated)
                effectiveness *= penaltyFactor;

            // Penalty 2: Aircraft heading toward the missile
            Vector3 aircraftForward = sourceAircraft.transform.forward;
            Vector3 aircraftToMissile = (seekerPosition - sourceAircraft.transform.position).normalized;
            float dot = Vector3.Dot(aircraftForward, aircraftToMissile);

            // dot > 0 means heading toward the missile
            // dot ~ 0 means notching (perpendicular)
            // dot < 0 means heading away
            if (dot > 0.1f)
                effectiveness *= penaltyFactor;

            return effectiveness;
        }

        /// <summary>
        /// Checks if this decoy should attract a missile that's currently tracking the source aircraft.
        /// Instead of a hard angle cutoff, the decoy's effectiveness is reduced to 25% when the
        /// aircraft is heading toward the missile or has its radar on. This means the decoy still
        /// has a chance of working but requires multiple decoys to overcome the penalty.
        /// Notching or turning away gives full effectiveness.
        /// </summary>
        public bool ShouldAttractMissile(
            Vector3 seekerPosition, Unit currentTarget, RadarParams radarParams)
        {
            if (!isActive) return false;
            if ((Object)sourceAircraft == null) return false;
            if ((Object)currentTarget == null) return false;

            // Only attract missiles targeting our source aircraft
            if ((Object)currentTarget != (Object)sourceAircraft)
                return false;

            float distToDecoy = Vector3.Distance(seekerPosition, this.transform.position);
            float distToTarget = Vector3.Distance(seekerPosition, currentTarget.transform.position);

            // Decoy must be within reasonable range of the seeker
            if (distToDecoy > radarParams.maxRange) return false;

            // Check line of sight to decoy (layer 64 = terrain)
            if (Physics.Linecast(seekerPosition, this.transform.position, 64))
                return false;

            // Calculate effectiveness based on aircraft behavior
            float effectiveness = GetEffectiveness(seekerPosition);

            // Compare radar returns.
            // Effectiveness scales the threshold: when penalized, the decoy's return
            // must be proportionally stronger to overcome the aircraft's real return.
            // We divide the target return by effectiveness rather than multiplying the
            // decoy return, because the RCS fourth-root means even a 3x RCS advantage
            // only gives ~1.3x signal advantage — multiplying that by 0.5 would make
            // the decoy always lose.
            float decoyReturn = GetDecoyRadarReturn(seekerPosition, radarParams);
            float targetReturn = radarParams.maxRange / Mathf.Max(distToTarget, 1f)
                * Mathf.Pow(currentTarget.RCS, 0.25f);
            targetReturn = Mathf.Min(targetReturn, radarParams.maxSignal);

            // When effectiveness < 1, the target appears stronger to the comparison,
            // requiring the decoy to have a bigger advantage to win
            float adjustedThreshold = targetReturn / Mathf.Max(effectiveness, 0.01f);

            return decoyReturn > adjustedThreshold;
        }
    }
}
