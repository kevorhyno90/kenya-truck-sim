using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Single source of truth for all colors across the Kenyan town world,
    /// lighting, vehicles, pedestrians, traffic, and uGUI shell/HUD.
    /// </summary>
    public static class Palette
    {
        // Sky, Sun & Atmosphere (Warm Kenyan Mid-Afternoon)
        public static readonly Color SunKey = Hex("#FFF1D6");
        public static readonly Color AmbientSky = Hex("#7AA7D9");
        public static readonly Color AmbientEquator = Hex("#D8C8B0");
        public static readonly Color AmbientGround = Hex("#4A3E33");
        public static readonly Color FogColor = Hex("#C9D6E6");
        public static readonly Color SkyTint = Hex("#5D90C4");
        public static readonly Color SkyGround = Hex("#6B5B4D");

        // Streets, Roads & Curbs
        public static readonly Color Tarmac = Hex("#262B33");
        public static readonly Color TarmacShoulder = Hex("#343B46");
        public static readonly Color Sidewalk = Hex("#C8C2B8");
        public static readonly Color CurbStone = Hex("#9E988E");
        public static readonly Color RoadLineWhite = Hex("#F2EFE9");
        public static readonly Color RoadLineYellow = Hex("#F5B82E");
        public static readonly Color TownGround = Hex("#7F7463");
        public static readonly Color GrassVerge = Hex("#5A7C44");

        // Kenyan Town Facades & Shops
        public static readonly Color FacadeCream = Hex("#E7D9C4");
        public static readonly Color FacadeTerracotta = Hex("#D98C5F");
        public static readonly Color FacadeOchre = Hex("#DFB46A");
        public static readonly Color FacadeSage = Hex("#A6B89E");
        public static readonly Color FacadeSlateBlue = Hex("#7998B3");
        public static readonly Color FacadeWarmGrey = Hex("#C7BFB5");
        public static readonly Color TrimDark = Hex("#3A3F47");
        public static readonly Color RoofIron = Hex("#6D5447");
        public static readonly Color WaterTankBlack = Hex("#1F242B");
        public static readonly Color WaterTankBlue = Hex("#1F5B8C");
        public static readonly Color GlassWindow = Hex("#2C445C");
        public static readonly Color WindowFrame = Hex("#4B525E");

        public static readonly Color AwningRed = Hex("#C63B2F");
        public static readonly Color AwningGreen = Hex("#2E7D4F");
        public static readonly Color AwningBlue = Hex("#2A6496");
        public static readonly Color AwningGold = Hex("#D98F24");
        public static readonly Color ShopSignBoard = Hex("#1F2A38");

        // Street Furniture & Nature
        public static readonly Color FoliageRich = Hex("#4B7A3B");
        public static readonly Color FoliageHighlight = Hex("#62944B");
        public static readonly Color TreeTrunk = Hex("#634A36");
        public static readonly Color PlanterSoil = Hex("#523E2E");
        public static readonly Color LampPostMetal = Hex("#3B424D");
        public static readonly Color LampGlow = Hex("#FFF3CC");
        public static readonly Color BusShelterFrame = Hex("#263445");
        public static readonly Color BusShelterRoof = Hex("#D42A2A");
        public static readonly Color BenchWood = Hex("#8F5E38");
        public static readonly Color StageZoneAccent = Hex("#F5A623");
        public static readonly Color StageZoneComplete = Hex("#22C55E");

        // Traffic Signals
        public static readonly Color SignalHousing = Hex("#1D2228");
        public static readonly Color SignalRedOn = Hex("#FF3B30");
        public static readonly Color SignalYellowOn = Hex("#FFCC00");
        public static readonly Color SignalGreenOn = Hex("#22C55E");
        public static readonly Color SignalOff = Hex("#282D34");

        // Player Fleet: ENA Coach KDK 057M & EPH Isuzu FRR 90
        public static readonly Color EnaBodyWhite = Hex("#F7F8FA");
        public static readonly Color EnaSkirtRed = Hex("#D42A2A");
        public static readonly Color EnaStripeBlack = Hex("#1A1E24");
        public static readonly Color EnaLogoOrange = Hex("#F58220");
        public static readonly Color EnaChrome = Hex("#DCE2EA");
        public static readonly Color EnaGlassDark = Hex("#182433");
        public static readonly Color HeadlightWarm = Hex("#FFF5D6");
        public static readonly Color TaillightRed = Hex("#E52828");
        public static readonly Color TireRubber = Hex("#1C1F24");
        public static readonly Color WheelRimAlloy = Hex("#C8CFD8");
        public static readonly Color PlateYellow = Hex("#F2C94C");
        public static readonly Color CabInterior = Hex("#2D323A");

        public static readonly Color IsuzuCabNavy = Hex("#1A355B");
        public static readonly Color IsuzuCargoSilver = Hex("#D4D8DF");
        public static readonly Color IsuzuStripeGold = Hex("#E09F24");
        public static readonly Color BullbarSteel = Hex("#2B3038");

        // Passing AI Traffic (Left-Hand Traffic)
        public static readonly Color MatatuWhite = Hex("#EFECE6");
        public static readonly Color MatatuStripeYellow = Hex("#F2B705");
        public static readonly Color MatatuAccentTeal = Hex("#1F8A8C");
        public static readonly Color CarSilver = Hex("#B8C0CA");
        public static readonly Color CarMaroon = Hex("#7A2E3A");
        public static readonly Color CarBlue = Hex("#2C5D8F");
        public static readonly Color CarGreen = Hex("#376B4B");
        public static readonly Color TruckCabWhite = Hex("#E4E6EA");
        public static readonly Color TruckCargoBlue = Hex("#28507A");

        // Pedestrians
        public static readonly Color[] SkinTones =
        {
            Hex("#5E3A24"),
            Hex("#7A4E32"),
            Hex("#4A2C1B"),
            Hex("#6B442A")
        };

        public static readonly Color[] ShirtColors =
        {
            Hex("#D9534F"),
            Hex("#2E86AB"),
            Hex("#E6A23C"),
            Hex("#45936C"),
            Hex("#7B5EA7"),
            Hex("#E8DFD3"),
            Hex("#C86D44")
        };

        public static readonly Color[] TrouserColors =
        {
            Hex("#2A303B"),
            Hex("#9E8B6E"),
            Hex("#3B5478"),
            Hex("#3D352E")
        };

        public static readonly Color[] BuildingFacades =
        {
            FacadeCream,
            FacadeTerracotta,
            FacadeOchre,
            FacadeSage,
            FacadeSlateBlue,
            FacadeWarmGrey
        };

        public static readonly Color[] ShopAwnings =
        {
            AwningRed,
            AwningGreen,
            AwningBlue,
            AwningGold
        };

        // uGUI Shell & HUD (Dark-Warm Charcoal + Amber/Red/Emerald — never pure black or default gray)
        public static readonly Color UiBackdropScrim = WithAlpha(Hex("#141922"), 0.74f);
        public static readonly Color UiPanelDark = WithAlpha(Hex("#1B222E"), 0.90f);
        public static readonly Color UiPanelSoft = WithAlpha(Hex("#222B3A"), 0.84f);
        public static readonly Color UiPanelBorder = Hex("#3A4659");
        public static readonly Color UiTextPrimary = Hex("#F5F2EB");
        public static readonly Color UiTextMuted = Hex("#B4BECB");
        public static readonly Color UiAccentAmber = Hex("#F5A623");
        public static readonly Color UiAccentRed = Hex("#D42A2A");
        public static readonly Color UiAccentGreen = Hex("#22C55E");
        public static readonly Color UiButtonNormal = Hex("#273345");
        public static readonly Color UiButtonHover = Hex("#34445C");
        public static readonly Color UiButtonPressed = Hex("#1D2634");
        public static readonly Color UiBarTrack = Hex("#2A3342");
        public static readonly Color UiFadeCurtain = Hex("#121720");

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                return c;
            }
            return new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
