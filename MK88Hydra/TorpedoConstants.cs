using UnityEngine;

namespace Hydra
{
    /// <summary>Stable keys and tuning for MK-88 Hydra (json keys keep mk54 for save/mount compat).</summary>
    internal static class TorpedoConstants
    {
        public const string MissileJsonKey = "missilepack_mk54_torpedo";
        public const string MountJsonKey = "MissilePack_MK54_Torpedo_single";
        public const string MountJsonKeyDouble = "MissilePack_MK54_Torpedo_double";
        public const string WeaponInfoName = "MK-88 Hydra";
        public const string MountDisplayName = "MK-88 Hydra";
        public const string UnitName = "MK-88 Hydra";
        public const string ShortName = "MK-88";
        public const string ShortNameLegacy = "MK54";
        public const string BogeyName = "Hydra";

        public const string PreviewIconFileName = "PreviewHydra.png";
        public const string PreviewIconResource = "Hydra.Resources.PreviewHydra.png";
        public const int PreviewIconInkMin = 12;
        public const int PreviewIconAlphaBase = 255;
        public const int PreviewIconStrokeRadius = 3;

        // Hydroacoustic decoy traps (per-ship counts, 7% redirect each).
        public const float DecoyTrapRedirectChance = 0.07f;
        public const float DecoyFieldRadiusM = 450f;
        public const float DecoyBubbleDepthM = 3f;
        public const float DecoyBubbleLifetimeS = 1.2f;
        public const float DecoyBubbleSpeedMps = 0.35f;
        public const float DecoyBubbleSizeM = 0.18f;
        public const float DecoyBubbleIntervalS = 0.45f;
        public const int DecoyCountAnnex = 130;
        public const int DecoyCountHyperion = 130;
        public const int DecoyCountDynamo = 70;
        public const int DecoyCountArgus = 70;
        public const int DecoyCountShard = 30;
        public const int DecoyCountCursor = 50;

        // Full hung model envelope (was 3.9×1.45 visual vs 3.9 encyclopedia).
        public const float LengthM = 5.65f;
        public const float WidthM = 0.87f;
        public const float HeightM = 0.87f;
        public const float VisualScaleMult = 1f;
        // Tiny gap under pylon after PlaceOfRocketLock snap (meters)
        public const float MountClearanceM = 0.05f;
        // After bay COM snap: leave at most this much below hardpoint (world Y), lift the rest up.
        public const float BayBottomSlackM = 0.12f;
        public const float BayCenterLiftExtraM = 0.15f;
        public const float MassKg = LaunchMassKg;
        public const float TorpedoCoreMassKg = 2000f;
        public const float KozuchMassKg = 300f;
        public const float ParachuteMassKg = 50f;
        public const float LaunchMassKg = TorpedoCoreMassKg + KozuchMassKg + ParachuteMassKg;
        // bomb_glide1-scale mass for ApplyAero only (ForceMode.Force); identity stays LaunchMassKg
        public const float ShellAeroMassKg = 125f;
        public const float BlastYieldKg = 450f;
        // UW: stamp TBM Shockwave FX on warhead (vanilla damage path).
        public const string SeekerTypeName = "Sonar";
        // UnitConverter.ValueReading expects millions of $ → displays $3.9m
        public const float Cost = 3.9f;
        // Scaled from 0.15@0.6m dia for ~0.87m dia × 5.65m body
        public const float RadarSize = 0.45f;

        // Physics/network shell: glide munition (Down-rail). NEVER BallisticMissile1 / Piledriver / tacNuke
        public const string ShellMissileKey = "bomb_glide1";
        public const string ShellMissileKeyAlt = "bomb_250";
        public const string ShellMountKey = "bomb_glide1_triple";
        public const string ShellMountKeyAlt = "bomb_250_internal";

        // Slot markers only (read-only — never Instantiate / mutate these mounts)
        public const string CarrierDarkreach = "Darkreach";
        public const string CarrierAlkyon = "FastBomber1";
        public const string PiledriverHePrefix = "BallisticMissile1_";
        public const string PiledriverNukeToken = "tacNuke";

        // Bundle / Blueprinter
        public const string BundleModName = "MK88Hydra";
        public const string MeshPrefabAsset = "TorpedoVisual";
        public const string NobpFileName = "MK88Hydra.nobp";
        public const string KozuchTextureFileName = "KozuchTorpedoTexture.png";

        // Extra Euler on TorpedoVisual after auto-align longest→+Z (tune if nose/roll wrong)
        public static readonly Vector3 VisualEulerDeg = new Vector3(0f, 0f, 0f);

        // EN (Blender export) + legacy RU
        public static readonly string[] AttachPylonAliases =
        {
            "PlaceOfRocketLock", "Attach_Pylon", "Attach_Bay", "Pylon", "Mount", "Hardpoint",
            "МестоКрепленияТорпеды", "КрепленияТорпеды", "Крепления"
        };
        public static readonly string[] AttachParachuteAliases =
        {
            "PlaceOfSpawnParachute", "Attach_Parachute", "ChuteAttach",
            "МестоСпавнаПарашюта"
        };
        public static readonly string[] ParachuteCoverAliases =
        {
            "ParachuteBox", "КонтейнерПарашюта"
        };
        public static readonly string[] PropCwAliases =
        {
            "RotorWing1", "Prop_CW", "PropCW", "Screw_CW", "Rotor_CW",
            "Лопасть1поЧасовой", "поЧасовой"
        };
        public static readonly string[] PropCcwAliases =
        {
            "RotorWing2", "Prop_CCW", "PropCCW", "Screw_CCW", "Rotor_CCW",
            "Лопасть2противЧасовой", "противЧасовой"
        };
        public static readonly string[] KozuchAliases =
        {
            "KozuchTorpedos", "Kozuch",
            "КожухТорпеды", "Кожух"
        };
        public static readonly string[] FinAliases =
        {
            "WingL", "WingR",
            "КрылоЛ", "КрылоП"
        };

        // Air glide: slope from remaining alt/range. Never down-rail forward, never extra sink accel.
        public const float GlideStabilizeSeconds = 1f;
        public const float GlideCruisePitchMaxDeg = 12f;
        public const float GlidePitchUpMaxDeg = 3f;
        public const float GlideMaxSpeedMps = 110f;
        public const float GlideMinSpeedMps = 80f;
        public const float GlideSteerDegS = 18f;
        public const float GlideAlignDegS = 120f;
        public const float GlideDragAccel = 0.018f;
        public const float GlideAngularDrag = 8f;
        public const float FinDeployAngleDeg = 70f;
        public const float MinGlideSecondsBeforeShed = 8f;
        public const float MinAirGlideDistM = 3000f;
        // Soft descent cue via aimpoint only (~6k ft/min slope hint)
        public const float SoftSinkFpm = 6000f;
        public const float SoftSinkMps = SoftSinkFpm * 0.3048f / 60f;
        // TZ altitudes (sea gap / radar)
        public const float ShedKozuchAltitudeM = 500f;
        public const float ParachuteDeployAltitudeM = 400f;
        public const float ChuteBoxJettisonAltitudeM = 30f;
        public const float AltitudeGateSlackM = 15f;
        public const float ShedReadyAltitudeM = ShedKozuchAltitudeM;
        public const float ShedAltitudeM = ShedKozuchAltitudeM;
        public const float ChuteOpenAltitudeMaxM = 420f;
        public const float ChuteOpenAltitudeMinM = 350f;
        public const float ChuteEmergencyAltM = 80f;
        public const float ChuteOpenDelayMinS = 0.2f;
        public const float ChuteCutAltitudeM = ChuteBoxJettisonAltitudeM;
        public const float ChuteMaxFallMps = 22f;
        // GPO-N (1.5kt) donor radius × this — not absolute 4m and not massScale×16 mesh
        public const float ChuteRadiusScaleFromDonor = 3f;
        public const float ChuteMaxRadiusM = 4f; // fallback only if donor unread
        public const float ChuteMaxDrag = 55f;
        public const float ChuteLineSpring = 220f;
        public const float ChuteDamping = 2.8f;
        public const float ChuteAftOffsetM = 2.2f;
        // Canopy locked this far aft of line root along −hull.forward
        public const float ChuteLineLengthM = 3.5f;
        public const float ChuteAttachMinAftM = 0.8f;
        // Over land glide: hold at least this sea-gap / map clearance until coast
        public const float LandGlideHoldAltM = 400f;
        public const float LandGlideClearanceM = 180f;
        public const float ChuteCanopyMassKg = 18f;
        public const float ChuteBodyDrag = 0.55f;
        public const float ChuteBodyAngularDrag = 40f; // hold attitude — chute must not spin the hull
        public const float ChuteMaxAngVelRad = 0.35f;
        public const float ChuteInflatePerSec = 1.5f;
        public const float ChuteDeployImpulse = 28f; // initial canopyVel along arrow only
        public const float ChuteFakeOpenSpeed = 35f; // unused — kept for compat
        public const float BallisticDrag = 0.05f;
        public const float BallisticAngularDrag = 28f;
        public const float BallisticTumbleDegS = 25f;
        public const float BallisticAlignDegS = 35f;
        public const float BallisticMaxAngVelRad = 1.0f;
        public const float BallisticAngVelDamp = 0.82f;
        public const float ChuteAlignDegS = 0f;

        // --- Underwater hydro ---
        // Soft top speed from thrust↔quadratic drag balance (not a hard km/h clamp).
        public const float WaterDensity = 1025f;
        public const float SwimCdArea = 0.08f;
        public const float SwimSideDamp = 4.5f;
        public const float SwimHeaveDamp = 3.5f;
        public const float SwimSurgeDamp = 0f;
        // Design equilibrium ≈ SwimSpeedKmh: T = ½ ρ v² CdA
        public const float SwimSpeedKmh = 250f;
        public const float SwimSpeedMps = SwimSpeedKmh / 3.6f;
        public const float SwimPropThrustN =
            0.5f * WaterDensity * SwimSpeedMps * SwimSpeedMps * SwimCdArea;
        public const float SwimPropStaticMult = 1f;
        public const float SwimPropCruiseMult = 1f;
        public const float SwimPropThrustGain = 6f;
        public const float SwimPropThrustMax = 120f;
        public const float SwimOverSpeedBrake = 0f;
        public const float SwimFinAuthority = 0.0002f;
        public const float TerminalFinMult = 1.15f;
        public const float SwimMaxAngVelRad = 1.8f;
        public const float SwimLinearDrag = 850f;
        public const float SwimThrustRampS = 28f;
        public const float SwimEntryHorizCapMps = 42f;
        public const float SwimAngularDrag = 2.5f;
        public const float SwimThrustGain = 6f;
        public const float SwimBuoyancyGain = 2.8f;
        public const float SwimDepthM = 4f;
        public const float SwimTurnRateDeg = 45f;       // legacy
        public const float PropRpm = 480f;
        public const float FinFoldSeconds = 1f;
        public const float SoftKillTimeoutS = 900f;
        public const float DetonateProximityM = 8f;
        public const float TerminalRangeM = 300f;
        public const float TerminalTurnRateDeg = 90f;
        public const float TerminalSpeedMult = 1f;
        public const float InterceptLeadMaxS = 12f;
        public const float WaterEntrySubmergeM = 1f;
        public const float SeaHitSlackM = 0.75f;
        public const float SeaSurfaceBandM = 2.5f;
        public const float SwimAlignDegS = 90f;

        // Water entry ~25km from ship (TZ). Air flies to that point; swim intercepts ship.
        public const float RouteEntryStandoffM = 25000f;
        public const float RouteEntryStandoffMinM = 2000f;
        public const float RouteEntryRingSlackM = 800f;
        public const float RouteAirGlideReachMarginM = 500f;
        public const float RouteShedApproachM = 4000f;
        public const float RouteChuteApproachM = 2500f;
        public const float RouteFallbackRunM = 2000f;
        public const float SwimFuelRangeM = 70000f;
        public const float FallbackAirGlideRangeM = 50000f;
        public const float ShipSearchRangeM = FallbackAirGlideRangeM + SwimFuelRangeM;
        public const float RouteShoreSlackM = 1.5f;
        public const float RouteMinKeelClearM = 6f;
        public const float ImpactRadiusM = 0.39f; // WidthM * 0.45
        public const float ImpactLookMinM = 0.15f;
    }
}
