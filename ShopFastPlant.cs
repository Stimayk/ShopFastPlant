using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopFastPlant
{
    public class ShopFastPlant : BasePlugin
    {
        public override string ModuleName => "[SHOP] Fast Plant";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "FastPlant";
        public static JObject? JsonFastPlant { get; private set; }
        private readonly PlayerFastPlant[] playerFastPlants = new PlayerFastPlant[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/FastPlant.json");
            if (File.Exists(configPath))
            {
                JsonFastPlant = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonFastPlant == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Быстрая установка C4");

            foreach (var item in JsonFastPlant.Properties().Where(p => p.Value is JObject))
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(
                        item.Name,
                        (string)item.Value["name"]!,
                        CategoryName,
                        (int)item.Value["price"]!,
                        (int)item.Value["sellprice"]!,
                        (int)item.Value["duration"]!
                    );
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerFastPlants[playerSlot] = null!);

            RegisterEventHandler<EventBombBeginplant>((@event, info) =>
            {
                var player = @event.Userid;
                if (player == null || !player.IsValid || playerFastPlants[player.Slot] == null) return HookResult.Continue;

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn == null || !player.PawnIsAlive) return HookResult.Continue;

                var weaponService = playerPawn.WeaponServices?.ActiveWeapon;
                if (weaponService == null) return HookResult.Continue;

                var activeWeapon = weaponService.Value;
                if (activeWeapon == null) return HookResult.Continue;

                if (!activeWeapon.DesignerName.Contains("c4")) return HookResult.Continue;

                CC4 c4 = new(activeWeapon.Handle)
                {
                    ArmedTime = Server.CurrentTime
                };

                return HookResult.Continue;
            });
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            playerFastPlants[player.Slot] = new PlayerFastPlant(itemId);
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1)
            {
                playerFastPlants[player.Slot] = new PlayerFastPlant(itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerFastPlants[player.Slot] = null!;
            return HookResult.Continue;
        }

        public record class PlayerFastPlant(int ItemID);
    }
}