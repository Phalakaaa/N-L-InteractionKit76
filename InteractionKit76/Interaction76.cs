using Life;
using Life.BizSystem;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Internal;
using ModKit.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace InteractionKit
{
    public class InteractionKit : ModKit.ModKit
    {
        public static System.Random rand;

        public InteractionKit(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.1", "Phalakaa");
            rand = new System.Random();
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitEntities();
            InsertMenu();
            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public void InitEntities()
        {
            Orm.RegisterTable<InteractionKitCooldown>();
        }

        public void InsertMenu()
        {
            _menu.AddDocumentTabLine(PluginInformations, "Regarder votre C.I", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                Character characterJson = player.GetCharacterJson();
                player.setup.TargetCreateCNI(characterJson);
            });

            _menu.AddDocumentTabLine(PluginInformations, "Montrer votre C.I", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                var target = player.GetClosestPlayer();
                if (target != null)
                {
                    Character characterJson = player.GetCharacterJson();
                    target.setup.TargetCreateCNI(characterJson);
                }
                else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
            });

            _menu.AddDocumentTabLine(PluginInformations, "Regarder votre permis B", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                if (player.character.PermisB) LookDrivingLicense(player);
                else player.Notify("Échec", "Vous ne possedez pas le permis B", NotificationManager.Type.Error);
            });

            _menu.AddDocumentTabLine(PluginInformations, "Montrer votre permis B", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                if (player.character.PermisB)
                {
                    var toPlayer = player.GetClosestPlayer();
                    if (toPlayer != null) ShowDrivingLicense(toPlayer, player);
                    else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                }
                else player.Notify("Échec", "Vous ne possedez pas le permis B", NotificationManager.Type.Error);
            });

            _menu.AddInteractionTabLine(PluginInformations, "Fouiller", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                if (await UpdateCooldown(player, nameof(InteractionKitCooldown.LastFrisked)))
                {
                    var target = player.GetClosestPlayer();

                    if (target != null)
                    {
                        if (target.Health <= 0)
                        {
                            player.setup.TargetOpenPlayerInventory(target.netId);
                            target.Notify("Interaction", "Une personne vous fouille", NotificationManager.Type.Warning, 6);
                            player.Notify("Échec", "Nous n'avons pas pu accéder à l'inventaire de votre cible", NotificationManager.Type.Error);
                        }
                        else ToBeFrisked(target, player);
                    }
                    else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                }
            });

            _menu.AddInteractionTabLine(PluginInformations, "Faire les poches", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                var target = player.GetClosestPlayer();

                if (await UpdateCooldown(player, nameof(InteractionKitCooldown.LastSteal)))
                {
                    if (target != null)
                    {
                        if (target.character.Money > 0)
                        {
                            int max = 10;
                            int success = 33;
                            int roll = rand.Next(0, 101);
                            if (roll <= success)
                            {
                                double percent = rand.Next(1, max + 1) / 100.0;
                                int amount = (int)Math.Floor(target.character.Money * percent);
                                if (amount > 0)
                                {
                                    target.AddMoney(-amount, "steal");
                                    player.AddMoney(amount, "steal");
                                    player.Notify("Interaction", $"Vous venez de voler {amount}€ en toute discrétion", NotificationManager.Type.Success, 6);
                                }
                                else
                                {
                                    player.Notify("Interaction", $"Il n'y avait rien à voler", NotificationManager.Type.Warning, 6);
                                }
                            }
                            else
                            {
                                target.Notify("Interaction", $"Quelqu'un vient de glisser sa main dans votre poche !", NotificationManager.Type.Warning, 6);
                                player.Notify("Interaction", $"Votre cible s'aperçoit qu'une main vient de se glisser dans sa poche", NotificationManager.Type.Warning, 6);
                            }
                        }
                    }
                    else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                }
            });

            _menu.AddInteractionTabLine(PluginInformations, "Assommer", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                if (await UpdateCooldown(player, nameof(InteractionKitCooldown.LastKnockedOut)))
                {
                    var target = player.GetClosestPlayer();

                    if (target != null) ToBeKnockedOut(target, player);
                    else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                }
            });

            _menu.AddInteractionTabLine(PluginInformations, "Attacher/Détacher", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                if (await UpdateCooldown(player, nameof(InteractionKitCooldown.LastRestrain)))
                {
                    var target = player.GetClosestPlayer();

                    if (target != null)
                    {
                        ToBeRestrain(target, player);
                    }
                    else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                }

            });

            _menu.AddInteractionTabLine(PluginInformations, "Premiers secours", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                if (player.HasBiz())
                {
                    Activity.Type playerBizType = Nova.biz.GetBizActivities(player.biz.Id).FirstOrDefault();
                    if (playerBizType == Activity.Type.Medical)
                    {
                        var target = player.GetClosestPlayer();

                        if (target != null)
                        {
                            if (target.Health <= 0)
                            {
                                player.setup.TargetShowCenterText("Premiers secours", "Vous appliquez les gestes de premiers secours", 5);
                                target.setup.TargetShowCenterText("Premiers secours", "Une personne à proximité applique les gestes de premiers secours", 5);
                                await Task.Delay(5000);
                                target.Health = 10;
                            }
                            else player.Notify("Échec", "Aucun citoyen inconscient à proximité", NotificationManager.Type.Error);
                        }
                        else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
                    }
                    else player.Notify("Échec", "Vous ne possédez pas la compétence \"Premiers secours\"", NotificationManager.Type.Error);
                }
                else player.Notify("Échec", "Vous ne possédez pas la compétence \"Premiers secours\"", NotificationManager.Type.Error);
            });
        }
        #region PANELS
        public void LookDrivingLicense(Player player)
        {
            Panel panel = PanelHelper.Create($"Permis B", UIPanel.PanelType.Text, player, () => LookDrivingLicense(player));

            panel.TextLines.Add($"{mk.Color("Nom:", mk.Colors.Info)} {player.character.Lastname}");
            panel.TextLines.Add($"{mk.Color("Prénom:", mk.Colors.Info)} {player.character.Firstname}");
            panel.TextLines.Add($"{mk.Color("Date de naissance:", mk.Colors.Info)}  {player.character.Birthday}");
            panel.TextLines.Add($"{mk.Color("Points:", mk.Colors.Info)} {player.character.PermisPoints}/12");

            panel.AddButton("Retour", ui => AAMenu.AAMenu.menu.DocumentPanel(player));
            panel.CloseButton();

            panel.Display();
        }
        public void ShowDrivingLicense(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Permis B", UIPanel.PanelType.Text, toPlayer, () => ShowDrivingLicense(fromPlayer, toPlayer));

            panel.TextLines.Add($"{mk.Color("Nom:", mk.Colors.Info)} {fromPlayer.character.Lastname}");
            panel.TextLines.Add($"{mk.Color("Prénom:", mk.Colors.Info)} {fromPlayer.character.Firstname}");
            panel.TextLines.Add($"{mk.Color("Date de naissance:", mk.Colors.Info)}  {fromPlayer.character.Birthday}");
            panel.TextLines.Add($"{mk.Color("Points:", mk.Colors.Info)} {fromPlayer.character.PermisPoints}/12");

            panel.CloseButton();

            panel.Display();
        }
        public void ToBeFrisked(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Demande pour être fouillé", UIPanel.PanelType.Text, toPlayer, () => ToBeFrisked(toPlayer, fromPlayer));

            panel.TextLines.Add($"Une personne à proximité souhaite vous fouiller.");

            panel.CloseButtonWithAction("Accepter", async () =>
            {
                if (InventoryUtils.TargetOpenPlayerInventory(fromPlayer, toPlayer))
                {
                    fromPlayer.Notify("Interaction", "Votre cible accepte d'être fouillé", NotificationManager.Type.Warning, 6);
                    toPlayer.Notify("Interaction", "Vous avez accepté d'être fouillé", NotificationManager.Type.Warning, 6);
                    return await Task.FromResult(true);
                }
                else return await Task.FromResult(false);
            });

            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Interaction", "Votre cible refuse d'être fouillé", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Interaction", "Vous avez refusé d'être fouillé", NotificationManager.Type.Warning, 6);
                return await Task.FromResult(true);
            });

            panel.Display();
        }
        public void ToBeKnockedOut(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Demande pour être assommé", UIPanel.PanelType.Text, toPlayer, () => ToBeKnockedOut(toPlayer, fromPlayer));

            panel.TextLines.Add($"Une personne à proximité souhaite vous assommer.");

            panel.AddButton("Accepter", async (ui) =>
            {
                fromPlayer.Notify("Interaction", "Votre cible accepte d'être assommé", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Interaction", "Vous avez accepté d'être assommé", NotificationManager.Type.Warning, 6);

                panel.Close();

                toPlayer.Health = 0;
                toPlayer.setup.TargetShowCenterText("Assommé", "Vous subissez une perte de mémoire et oubliez les événements qui se sont déroulés au cours des 15 dernières minutes", 20);
                await Task.Delay(20000);
                toPlayer.Health = 20;
            });
            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Interaction", "Votre cible refuse d'être assommé", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Interaction", "Vous avez refusé d'être assommé", NotificationManager.Type.Warning, 6);
                return await Task.FromResult(true);
            });

            panel.Display();
        }
        public void ToBeRestrain(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Demande pour être {(toPlayer.setup.NetworkisRestrain ? "détaché" : "attaché")}", UIPanel.PanelType.Text, toPlayer, () => ToBeRestrain(toPlayer, fromPlayer));

            panel.TextLines.Add($"Une personne à proximité souhaite vous {(toPlayer.setup.NetworkisRestrain ? "détacher" : "attacher")}.");

            panel.CloseButtonWithAction("Accepter", async () =>
            {
                toPlayer.setup.NetworkisRestrain = !toPlayer.setup.NetworkisRestrain;
                return await Task.FromResult(true);
            });
            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Interaction", $"Votre cible refuse d'être {(toPlayer.setup.NetworkisRestrain ? "détaché" : "attaché")}", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Interaction", $"Vous avez refusé d'être {(toPlayer.setup.NetworkisRestrain ? "détaché" : "attaché")}", NotificationManager.Type.Warning, 6);
                return await Task.FromResult(true);
            });

            panel.Display();
        }
        #endregion

        public async Task<bool> UpdateCooldown(Player player, string interactionName)
        {
            long cooldown = 0;
            var query = await InteractionKitCooldown.Query(p => p.PlayerId == player.account.id);
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (query != null && query.Any())
            {
                var interactionCooldown = query[0];

                switch (interactionName)
                {
                    case nameof(InteractionKitCooldown.LastFrisked):
                        if ((currentTime - interactionCooldown.LastFrisked) > 60)
                        {
                            interactionCooldown.LastFrisked = (int)currentTime;
                            await interactionCooldown.Save();
                            return true;
                        }
                        else cooldown = currentTime - interactionCooldown.LastFrisked;
                        break;
                    case nameof(InteractionKitCooldown.LastSteal):
                        if ((currentTime - interactionCooldown.LastSteal) > 60)
                        {
                            interactionCooldown.LastSteal = (int)currentTime;
                            await interactionCooldown.Save();
                            return true;
                        }
                        else cooldown = currentTime - interactionCooldown.LastSteal;
                        break;
                    case nameof(InteractionKitCooldown.LastRestrain):
                        if ((currentTime - interactionCooldown.LastRestrain) > 60)
                        {
                            interactionCooldown.LastRestrain = (int)currentTime;
                            await interactionCooldown.Save();
                            return true;
                        }
                        else cooldown = currentTime - interactionCooldown.LastRestrain;
                        break;
                    case nameof(InteractionKitCooldown.LastKnockedOut):
                        if ((currentTime - interactionCooldown.LastKnockedOut) > 60)
                        {
                            interactionCooldown.LastKnockedOut = (int)currentTime;
                            await interactionCooldown.Save();
                            return true;
                        }
                        else cooldown = currentTime - interactionCooldown.LastKnockedOut;
                        break;
                    default:
                        Logger.LogError("UpdateCooldown", "L'interaction spécifiée n'est pas valide.");
                        return false;
                }

                player.Notify("Temps de recharge", $"Vous pourrez réutiliser cette interaction dans {60 - cooldown} secondes", NotificationManager.Type.Info);
                return false;
            }
            else
            {
                var newCooldown = new InteractionKitCooldown();
                newCooldown.PlayerId = player.account.id;
                await newCooldown.Save();
                return await UpdateCooldown(player, interactionName);
            }
        }

    }
}
