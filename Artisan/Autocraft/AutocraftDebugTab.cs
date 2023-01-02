using Artisan.CraftingLogic;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Autocraft
{
    internal unsafe static class AutocraftDebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;
        internal static void Draw()
        {
            ImGui.Checkbox("调试日志", ref Debug);
            if (ImGui.CollapsingHeader("工匠的食物"))
            {
                foreach (var x in ConsumableChecker.GetFood())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("物品栏中工匠的食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("物品栏中工匠的HQ食物"))
            {
                foreach (var x in ConsumableChecker.GetFood(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }
            if (ImGui.CollapsingHeader("工匠的药水"))
            {
                foreach (var x in ConsumableChecker.GetPots())
                {
                    ImGuiEx.Text($"{x.Id}: {x.Name}");
                }
            }
            if (ImGui.CollapsingHeader("物品栏中工匠的药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id);
                    }
                }
            }
            if (ImGui.CollapsingHeader("物品栏中工匠的HQ药水"))
            {
                foreach (var x in ConsumableChecker.GetPots(true, true))
                {
                    if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                    {
                        ConsumableChecker.UseItem(x.Id, true);
                    }
                }
            }

            if (ImGui.CollapsingHeader("生产统计"))
            {
                ImGui.Text($"当前耐久: {CurrentCraft.CurrentDurability}");
                ImGui.Text($"满耐久: {CurrentCraft.MaxDurability}");
                ImGui.Text($"当前进展: {CurrentCraft.CurrentProgress}");
                ImGui.Text($"满进展: {CurrentCraft.MaxProgress}");
                ImGui.Text($"当前品质: {CurrentCraft.CurrentQuality}");
                ImGui.Text($"满品质: {CurrentCraft.MaxQuality}");
                ImGui.Text($"物品名称: {CurrentCraft.ItemName}");
                ImGui.Text($"当前状态: {CurrentCraft.CurrentCondition}");
                ImGui.Text($"当前工次: {CurrentCraft.CurrentStep}");
                ImGui.Text($"阔步+比尔格连击: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"预测品质: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
            }
            ImGui.Separator();

            if (ImGui.Button("全部修复"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"装备耐久: {RepairManager.GetMinEquippedPercent()}");
            ImGuiEx.Text($"选择的配方: {AgentRecipeNote.Instance()->SelectedRecipeIndex}");
            ImGuiEx.Text($"材料不足: {HQManager.InsufficientMaterials}");

            /*ImGui.InputInt("id", ref SelRecId);
            if (ImGui.Button("OpenRecipeByRecipeId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)SelRecId);
            }
            if (ImGui.Button("OpenRecipeByItemId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByItemId((uint)SelRecId);
            }*/
            //ImGuiEx.Text($"Selected recipe id: {*(int*)(((IntPtr)AgentRecipeNote.Instance()) + 528)}");




        }
    }
}
