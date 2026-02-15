using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionActiveDecoy
{
    /// <summary>
    /// AN/ALQ-260(V)1 style expendable active decoy countermeasure.
    /// Launches a DRFM decoy that creates a false radar return stronger than the aircraft,
    /// causing radar-guided missiles (ARH/SARH) to switch lock onto the decoy.
    /// Unlike onboard jammers, this is off-board so it defeats home-on-jam.
    /// </summary>
    public class ActiveDecoyLauncher : Countermeasure
    {
        // Configurable parameters
        private float ejectionVelocity = 30f;
        private float ejectionInterval = 1.5f; // seconds between launches
        private int maxAmmo;
        private float lastEjectionTime;

        // Decoy parameters
        private float decoyRCSMultiplier = 3.0f; // decoy RCS = aircraft RCS * this
        private float decoyBaseRCS = 0.5f;       // minimum decoy RCS floor
        private float decoyLifetime = 12f;        // seconds the decoy stays active
        private float decoyDrag = 0.02f;

        // Sound and fire rate - borrowed from the aircraft's flare ejector
        private AudioClip ejectionSound;
        private float ejectionVolume = 1f;
        private AudioSource audioSource;
        private bool copiedFlareSettings;

        protected override void Awake()
        {
            base.Awake();
        }

        public override List<string> GetThreatTypes()
        {
            if (this.threatTypes == null)
                this.threatTypes = new List<string>() { "ARH", "SARH" };
            return this.threatTypes;
        }

        /// <summary>
        /// Lazily captures maxAmmo from the current ammo value.
        /// This is needed because Awake() runs during AddComponent before ammo is set externally.
        /// </summary>
        private void EnsureMaxAmmo()
        {
            if (this.maxAmmo <= 0 && this.ammo > 0)
                this.maxAmmo = this.ammo;
        }

        public override void Fire()
        {
            EnsureMaxAmmo();

            if (this.aircraft.disabled)
                return;

            if (Time.timeSinceLevelLoad - this.lastEjectionTime < this.ejectionInterval)
                return;

            if (this.ammo <= 0)
                return;

            this.lastEjectionTime = Time.timeSinceLevelLoad;
            this.ammo--;

            LaunchDecoy();

            this.aircraft.RequestRearm();

            if (GameManager.IsLocalAircraft(this.aircraft))
                this.UpdateHUD();
        }

        private void LaunchDecoy()
        {
            // Grab flare ejector settings (sound, volume, fire rate) if we haven't yet
            if (!this.copiedFlareSettings)
            {
                var flareEjector = this.aircraft.GetComponentInChildren<FlareEjector>();
                if (flareEjector != null)
                {
                    this.copiedFlareSettings = true;
                    var t = HarmonyLib.Traverse.Create(flareEjector);

                    var soundField = t.Field("ejectionSound");
                    if (soundField.FieldExists())
                        this.ejectionSound = soundField.GetValue<AudioClip>();

                    var volField = t.Field("ejectionVolume");
                    if (volField.FieldExists())
                        this.ejectionVolume = volField.GetValue<float>();

                    var intervalField = t.Field("ejectionInterval");
                    if (intervalField.FieldExists())
                        this.ejectionInterval = intervalField.GetValue<float>();
                }
            }

            // Calculate the decoy's effective RCS based on the launching aircraft.
            // If ActualStealth mod is installed, use the ORIGINAL (pre-division) RCS
            // so the decoy mimics what the aircraft would look like without stealth,
            // which is what a DRFM decoy actually does — it replays the radar pulse
            // as if reflecting off a larger target.
            float aircraftRCS = StealthModCompat.IsStealthModInstalled
                ? StealthModCompat.GetOriginalRCS(this.aircraft)
                : this.aircraft.RCS;
            float decoyRCS = Mathf.Max(aircraftRCS * decoyRCSMultiplier, decoyBaseRCS);

            // Create the decoy GameObject
            var decoyObj = new GameObject("ActiveDecoy");
            decoyObj.transform.position = this.aircraft.transform.position
                + this.aircraft.transform.forward * -5f
                + this.aircraft.transform.up * -2f;

            // Add the decoy behavior
            var decoy = decoyObj.AddComponent<ActiveDecoyBehavior>();
            decoy.Initialize(
                this.aircraft,
                this.aircraft.GetComponent<Rigidbody>().velocity
                    + this.aircraft.transform.forward * -ejectionVelocity
                    + this.aircraft.transform.up * -ejectionVelocity * 0.3f,
                decoyRCS,
                decoyLifetime,
                decoyDrag
            );

            // Play the flare ejection sound
            if (this.ejectionSound != null)
            {
                if (this.audioSource == null)
                {
                    var soundObj = new GameObject("ActiveDecoySFX");
                    soundObj.transform.SetParent(this.aircraft.transform);
                    soundObj.transform.localPosition = Vector3.zero;
                    this.audioSource = soundObj.AddComponent<AudioSource>();
                    this.audioSource.outputAudioMixerGroup = SoundManager.i.EffectsMixer;
                    this.audioSource.spatialBlend = 1f;
                    this.audioSource.dopplerLevel = 0f;
                    this.audioSource.spread = 5f;
                    this.audioSource.maxDistance = 40f;
                    this.audioSource.minDistance = 5f;
                    this.audioSource.volume = this.ejectionVolume;
                }
                this.audioSource.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
                this.audioSource.PlayOneShot(this.ejectionSound);
            }
        }

        public override void Rearm(Aircraft aircraft, Unit rearmer)
        {
            EnsureMaxAmmo();
            if (this.ammo == this.maxAmmo)
                return;
            this.ammo = this.maxAmmo;
            if (!GameManager.IsLocalAircraft(aircraft))
                return;
            this.UpdateHUD();
            SceneSingleton<AircraftActionsReport>.i.ReportText(
                "Active decoys rearmed by " + rearmer.unitName, 5f);
        }

        public override void UpdateHUD()
        {
            SceneSingleton<CombatHUD>.i.DisplayCountermeasures(
                this.displayName, this.displayImage, this.ammo);
        }

        public int GetAmmo() => this.ammo;
        public int GetMaxAmmo() => this.maxAmmo;
    }
}
