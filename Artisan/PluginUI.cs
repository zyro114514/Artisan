using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;

#if DEBUG
        public bool repeatTrial = false;
#endif
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        private static string? CurrentSelectedCraft;

        private readonly IDalamudPlugin Plugin;
        public PluginUI(Artisan plugin)
        {
            Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            //if (!CheckIfCorrectRepo())
            //{
            //    ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            //    if (ImGui.Begin("Fraudulant Repo Detected", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            //    {
            //        ImGui.Text("[Artisan] Please uninstall and use the official repo:");
            //        if (ImGui.Button("Repository"))
            //        {
            //            ImGui.SetClipboardText("https://love.puni.sh/ment.json");
            //            Notify.Success("Link copied to clipboard");
            //        }
            //        return;
            //    }
            //}

            DrawCraftingWindow();
            CraftingListUI.DrawProcessingWindow();

            if (!Handler.Enable)
            Handler.DrawRecipeData();

            //if (Service.Configuration.ShowEHQ)
            //    MarkChanceOfSuccess();

            if (!Service.Configuration.DisableHighlightedAction)
            Hotbars.MakeButtonsGlow(CurrentRecommendation);

            if (!Visible)
            {
                return;
            }

            ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan", ref visible, ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                if (ImGui.BeginTabBar("TabBar"))
                {
                    if (ImGui.BeginTabItem("设置"))
                    {
                        DrawMainWindow();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("耐力/自动重复模式"))
                    {
                        Handler.Draw();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("  宏  "))
                    {
                        MacroUI.Draw();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("制作清单 (BETA)"))
                    {
                        CraftingListUI.Draw();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("关于"))
                    {
                        PunishLib.ImGuiMethods.AboutTab.Draw(Plugin);
                        ImGui.EndTabItem();
                    }
#if DEBUG
                    if (ImGui.BeginTabItem("调试"))
                    {
                        AutocraftDebugTab.Draw();
                        ImGui.EndTabItem();
                    }
#endif
                    ImGui.EndTabBar();
                }
                if (!visible)
                {
                    Service.Configuration.Save();
                    PluginLog.Information("Configuration saved");
                }
            }
        }

        private bool CheckIfCorrectRepo()
        {
#if DEBUG
            return true;
#endif
            FileInfo? m = ECommons.DalamudServices.Svc.PluginInterface.AssemblyLocation;
            var manifest = Path.Join(m.DirectoryName, "Artison.json");
            if (File.Exists(manifest))
            {
                DRM? drm = JsonConvert.DeserializeObject<DRM>(File.ReadAllText(manifest));
                if (drm is null)
                    return false;

                if (!drm.DownloadLinkInstall.Equals(@"https://love.puni.sh/plugins/Artisan/latest.zip")) return false;
                if (!drm.Name.Equals("Artisan")) return false;

                return true;

            }
            else
            {
                return false;
            }
        }


        public unsafe static void MarkChanceOfSuccess()
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AtkUnitBase*)recipeWindow;
                if (addonPtr == null)
                    return;

                var baseX = addonPtr->X;
                var baseY = addonPtr->Y;

                var visCheck = (AtkComponentNode*)addonPtr->UldManager.NodeList[6];
                if (!visCheck->AtkResNode.IsVisible)
                    return;

                var selectedCraftNameNode = (AtkTextNode*)addonPtr->UldManager.NodeList[49];
                var selectedCraftWindowBox = (AtkTextNode*)addonPtr->UldManager.NodeList[31];
                var selectedCraftName = selectedCraftNameNode->NodeText.ToString()[14..];
                selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 10, 10);
                if (!char.IsLetterOrDigit(selectedCraftName[^1]))
                {
                    selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 1, 1).Trim();
                }
                CurrentSelectedCraft = selectedCraftName;
                string selectedCalculated = CalculateEstimate(selectedCraftName);
                AtkResNodeFunctions.DrawSuccessRate(&selectedCraftWindowBox->AtkResNode, selectedCalculated, selectedCraftName, true);
                AtkResNodeFunctions.DrawQualitySlider(&selectedCraftNameNode->AtkResNode, selectedCraftName);

                var craftCount = (AtkTextNode*)addonPtr->UldManager.NodeList[63];
                string count = craftCount->NodeText.ToString();
                int maxCrafts = Convert.ToInt32(count.Split("-")[^1]);

                var crafts = (AtkComponentNode*)addonPtr->UldManager.NodeList[67];
                if (crafts->AtkResNode.IsVisible)
                {
                    var currentShownNodes = 0;

                    for (int i = 1; i <= 13; i++)
                    {
                        var craft = (AtkComponentNode*)crafts->Component->UldManager.NodeList[i];
                        if (craft->AtkResNode.IsVisible && craft->AtkResNode.Y >= 0 && craft->AtkResNode.Y < 340 && currentShownNodes < 10 && currentShownNodes < maxCrafts)
                        {
                            currentShownNodes++;
                            var craftNameNode = (AtkTextNode*)craft->Component->UldManager.NodeList[14];
                            var ItemName = craftNameNode->NodeText.ToString()[14..];
                            ItemName = ItemName.Remove(ItemName.Length - 10, 10);
                            if (!char.IsLetterOrDigit(ItemName[^1]))
                            {
                                ItemName = ItemName.Remove(ItemName.Length - 1, 1).Trim();
                            }

                            string calculatedPercentage = CalculateEstimate(ItemName);

                            AtkResNodeFunctions.DrawSuccessRate(&craft->AtkResNode, $"{calculatedPercentage}", ItemName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeChance");
            }
        }

        private static string CalculateEstimate(string itemName)
        {
            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(itemName)).FirstOrDefault();
            if (sheetItem == null)
                return "Unknown Item - Check Selected Recipe Window";
            var recipeTable = sheetItem.RecipeLevelTable.Value;

            if (!sheetItem.ItemResult.Value.CanBeHq && !sheetItem.IsExpert && !sheetItem.ItemResult.Value.IsCollectable)
                return $"Item cannot be HQ.";

            if (CharacterInfo.Craftsmanship() < sheetItem.RequiredCraftsmanship || CharacterInfo.Control() < sheetItem.RequiredControl)
                return "Unable to craft with current stats.";

            if (CharacterInfo.CharacterLevel() >= 80 && CharacterInfo.CharacterLevel() >= sheetItem.RecipeLevelTable.Value.ClassJobLevel + 10 && !sheetItem.IsExpert)
                return "EHQ: Guaranteed.";

            var simulatedPercent = Service.Configuration.UseSimulatedStartingQuality && sheetItem.MaterialQualityFactor != 0 ? Math.Floor(((double)Service.Configuration.CurrentSimulated / ((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.QualityFactor / 100))) * 100) : 0;
            simulatedPercent = CurrentSelectedCraft is null || CurrentSelectedCraft != sheetItem.ItemResult.Value.Name!.RawString ? 0 : simulatedPercent;
            var baseQual = BaseQuality(sheetItem);
            var dur = recipeTable.Durability;
            var baseSteps = baseQual * (dur / 10);
            var maxQual = (double)recipeTable.Quality;
            bool meetsRecCon = CharacterInfo.Control() >= recipeTable.SuggestedControl;
            bool meetsRecCraft = CharacterInfo.Craftsmanship() >= recipeTable.SuggestedCraftsmanship;
            var q1 = baseSteps / maxQual;
            var q2 = CharacterInfo.MaxCP / sheetItem.QualityFactor / 1.5;
            var q3 = CharacterInfo.IsManipulationUnlocked() ? 2 : 1;
            var q4 = sheetItem.RecipeLevelTable.Value.Stars * 6;
            var q5 = meetsRecCon && meetsRecCraft ? 3 : 1;
            var q6 = Math.Floor((q1 * 100) + (q2 * 3 * q3 * q5) - q4 + simulatedPercent);
            var chance = q6 > 100 ? 100 : q6;
            chance = chance < 0 ? 0 : chance;

            return chance switch
            {
                < 20 => "EHQ: Do not attempt.",
                < 40 => "EHQ: Very low chance.",
                < 60 => "EHQ: Average chance.",
                < 80 => "EHQ: Good chance.",
                < 90 => "EHQ: High chance.",
                < 100 => "EHQ: Very high chance.",
                _ => "EHQ: Guaranteed.",
            };
        }

        private void DrawCraftingWindow()
        {
            if (!CraftingVisible)
            {
                return;
            }

            CraftingVisible = craftingVisible;

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan Crafting Window", ref this.craftingVisible, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                bool autoMode = Service.Configuration.AutoMode;

                if (ImGui.Checkbox("自动模式", ref autoMode))
                {
                    Service.Configuration.AutoMode = autoMode;
                    Service.Configuration.Save();
                }

                if (autoMode)
                {
                    var delay = Service.Configuration.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("设置延迟(ms)", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.AutoDelay = delay;
                        Service.Configuration.Save();
                    }
                }


                if (Handler.RecipeID != 0)
                ImGui.Checkbox("耐力模式", ref Handler.Enable);

                if (Service.Configuration.CraftingX && Handler.Enable)
                {
                    ImGui.Text($"Remaining Crafts: {Service.Configuration.CraftX}");
                }

#if DEBUG
                ImGui.Checkbox("重复制作练习", ref repeatTrial);
#endif
                //bool failureCheck = Service.Configuration.DisableFailurePrediction;

                //if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
                //{
                //    Service.Configuration.DisableFailurePrediction = failureCheck;
                //    Service.Configuration.Save();
                //}
                //ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

                ImGui.Text("半自动模式");

                if (ImGui.Button("执行建议的操作"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }
                if (ImGui.Button("获取建议"))
                {
                    Artisan.FetchRecommendation(CurrentStep);
                }



            }
            ImGui.End();
        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"在这里您可以更改Artisan将使用的一些设置。其中一些也可以在制作过程中切换。");
            ImGui.TextWrapped($"为了使用Artisan的手动高亮显示，请将您已解锁的每个制作技能放置在可见的热键栏中。");
            bool autoEnabled = Service.Configuration.AutoMode;
            //bool autoCraft = Service.Configuration.AutoCraft;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;
            //bool showEHQ = Service.Configuration.ShowEHQ;
            //bool useSimulated = Service.Configuration.UseSimulatedStartingQuality;
            bool useMacroMode = Service.Configuration.UseMacroMode;
            bool disableGlow = Service.Configuration.DisableHighlightedAction;
            bool disableToasts = Service.Configuration.DisableToasts;

            ImGui.Separator();
            if (ImGui.Checkbox("启用自动模式", ref autoEnabled))
            {
                Service.Configuration.AutoMode = autoEnabled;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"自动使用每个推荐的技能。\n需要技能位于可见的热键栏上。");
            if (autoEnabled)
            {
                var delay = Service.Configuration.AutoDelay;
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("设置延迟 (ms)", ref delay, 0, 1000))
                {
                    if (delay < 0) delay = 0;
                    if (delay > 1000) delay = 1000;

                    Service.Configuration.AutoDelay = delay;
                    Service.Configuration.Save();
                }
            }

            if (ImGui.Checkbox("禁用高亮显示框", ref disableGlow))
            {
                Service.Configuration.DisableHighlightedAction = disableGlow;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("这是在手动操作时在热键栏上高亮显示技能的框。");

            if (ImGui.Checkbox($"禁用推荐技能的提示信息", ref disableToasts))
            {
                Service.Configuration.DisableToasts = disableToasts;
                Service.Configuration.Save();
            }

            ImGuiComponents.HelpMarker("这些是每当推荐新技能时的弹出窗口提示。");

            //if (ImGui.Checkbox($"Automatically Repeat Last Craft", ref autoCraft))
            //{
            //    Service.Configuration.AutoCraft = autoCraft;
            //    Service.Configuration.Save();
            //}
            //ImGuiComponents.HelpMarker($"Repeats the currently selected craft in your recipe list.\nWill only work whilst you have the items.\nThis will repeat using your set item quality settings.");

            //if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
            //{
            //    Service.Configuration.DisableFailurePrediction = failureCheck;
            //    Service.Configuration.Save();
            //}
            //ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

            //if (ImGui.Checkbox("Show Estimated HQ on Recipe (EHQ)", ref showEHQ))
            //{
            //    Service.Configuration.ShowEHQ = showEHQ;
            //    Service.Configuration.Save();

            //}
            //ImGuiComponents.HelpMarker($"This will mark in the crafting list an estimated HQ chance based on your current stats.\nThis does not factor in any HQ items used as materials.\nIt is also only a rough estimate due to the nature of crafting.");

            //if (showEHQ)
            //{
            //    ImGui.Indent();
            //    if (ImGui.Checkbox("Use Simulated Starting Quality in Estimates", ref useSimulated))
            //    {
            //        Service.Configuration.UseSimulatedStartingQuality = useSimulated;
            //        Service.Configuration.Save();
            //    }
            //    ImGuiComponents.HelpMarker($"Set a starting quality as if you were using HQ items for calculating EHQ.");
            //    ImGui.Unindent();
            //}

            if (Service.Configuration.UserMacros.Count > 0)
            {
                if (ImGui.Checkbox("启用宏模式", ref useMacroMode))
                {
                    Service.Configuration.UseMacroMode = useMacroMode;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker(@"使用宏来制作而不是Artisan自己做决定。 
如果宏在制作完成之前结束，Artisan将继续做出决定，直到制作结束。
如果宏无法执行某个操作，您将不得不手动干预。");

                if (useMacroMode)
                {
                    string preview = Service.Configuration.SetMacro == null ? "" : Service.Configuration.SetMacro.Name;
                    if (ImGui.BeginCombo("选择宏", preview))
                    {
                        if (ImGui.Selectable(""))
                        {
                            Service.Configuration.SetMacro = null;
                            Service.Configuration.Save();
                        }
                        foreach (var macro in Service.Configuration.UserMacros)
                        {
                            bool selected = Service.Configuration.SetMacro == null ? false : Service.Configuration.SetMacro.ID == macro.ID;
                            if (ImGui.Selectable(macro.Name, selected))
                            {
                                Service.Configuration.SetMacro = macro;
                                Service.Configuration.Save();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
            }
            else
            {
                useMacroMode = false;
            }

            if (ImGui.Checkbox("在高品质时使用秘诀", ref useTricksGood))
            {
                Service.Configuration.UseTricksGood = useTricksGood;
                Service.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("在最高品质时使用秘诀", ref useTricksExcellent))
            {
                Service.Configuration.UseTricksExcellent = useTricksExcellent;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"这两个选项允许您在高品质或最高品质时优先使用秘诀。\n其他依赖这些状态的技能将不会被使用。");
            if (ImGui.Checkbox("使用专家技能", ref useSpecialist))
            {
                Service.Configuration.UseSpecialist = useSpecialist;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("如果当前的职业是专家，则花费您可能拥有的任何能工巧匠图纸。\n设计变动取代观察。");
            ImGui.TextWrapped("最高品质%%");
            ImGuiComponents.HelpMarker($"一旦品质达到以下百分比，Artisan将只关注进展。");
            if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"{maxQuality}%%"))
            {
                Service.Configuration.MaxPercentage = maxQuality;
                Service.Configuration.Save();
            }
        }
    }
}
