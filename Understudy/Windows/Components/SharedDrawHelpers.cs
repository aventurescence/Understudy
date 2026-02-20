using System;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Bindings.ImGui;

namespace Understudy.Windows.Components;

/// <summary>
/// Shared, stateless drawing utilities used across multiple UI components.
/// </summary>
internal static class SharedDrawHelpers
{
    internal static string GetSlotName(int id) => id switch
    {
        0 => "Main Hand", 1 => "Off Hand", 2 => "Head", 3 => "Body", 4 => "Hands",
        5 => "Waist", 6 => "Legs", 7 => "Feet", 8 => "Ears", 9 => "Neck",
        10 => "Wrists", 11 => "Ring (R)", 12 => "Ring (L)", _ => $"Slot {id}"
    };

    internal static string GetJobAbbreviation(uint jobId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        if (sheet != null && sheet.TryGetRow(jobId, out var job))
            return job.Abbreviation.ToString();
        return $"#{jobId}";
    }

    internal static string GetWorldName(uint worldId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>();
        if (sheet != null && sheet.TryGetRow(worldId, out var world))
            return world.Name.ToString();
        return $"#{worldId}";
    }

    internal static void DrawItemIcon(uint itemId, float size = 24f)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (sheet != null && sheet.TryGetRow(itemId, out var itemRow) && itemRow.Icon != 0)
        {
            var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(itemRow.Icon));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                ImGui.Image(wrap.Handle, new Vector2(size, size));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{itemRow.Name} (IL {itemRow.LevelItem.RowId})");
                return;
            }
        }

        ImGui.Dummy(new Vector2(size, size));
    }

    internal static void DrawIconWithCount(ushort iconId, float size, int count, string tooltip)
    {
        var countColor = count > 0 ? Theme.AccentWarning : Theme.TextDisabled;

        if (iconId != 0)
        {
            var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                var tint = count > 0 ? Vector4.One : new Vector4(0.4f, 0.4f, 0.4f, 0.5f);
                ImGui.Image(wrap.Handle, new Vector2(size, size), Vector2.Zero, Vector2.One, tint);

                var itemMax = ImGui.GetItemRectMax();
                var dl = ImGui.GetWindowDrawList();
                var countText = count.ToString();
                var textSize = ImGui.CalcTextSize(countText);
                var textPos = new Vector2(itemMax.X - textSize.X - 1, itemMax.Y - textSize.Y);

                dl.AddRectFilled(
                    textPos - new Vector2(2, 0),
                    textPos + textSize + new Vector2(2, 1),
                    ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f)),
                    2f);
                dl.AddText(textPos, ImGui.GetColorU32(countColor), countText);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{tooltip}: {count}");

                return;
            }
        }

        ImGui.TextColored(Theme.TextSecondary, $"{tooltip}:");
        ImGui.SameLine(0, 4);
        ImGui.TextColored(countColor, $"{count}");
    }

    internal static void DrawRaidBox(string name, bool isDone, uint imageId)
    {
        ImGui.TableNextColumn();

        if (imageId != 0)
        {
            var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(imageId));
            if (tex.TryGetWrap(out var wrap, out _))
            {
                var tint = isDone ? Vector4.One : new Vector4(0.25f, 0.25f, 0.30f, 0.6f);
                var imgWidth = ImGui.GetContentRegionAvail().X;
                ImGui.Image(wrap.Handle, new Vector2(imgWidth, 60), Vector2.Zero, Vector2.One, tint);

                if (isDone)
                {
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    var dl = ImGui.GetWindowDrawList();
                    dl.AddRect(min, max, ImGui.GetColorU32(Theme.AccentSuccess with { W = 0.6f }), 4f, ImDrawFlags.None, 2.0f);
                    
                    var texCheck = Plugin.TextureProvider.GetFromGame("ui/uld/Journal_Detail_hr1.tex");
                    if (!texCheck.TryGetWrap(out var checkWrap, out _))
                    {
                        texCheck = Plugin.TextureProvider.GetFromGame("ui/uld/Journal_Detail.tex");
                        texCheck.TryGetWrap(out checkWrap, out _);
                    }

                    if (checkWrap != null)
                    {
                        var uv0 = new Vector2(944f / checkWrap.Width, 96f / checkWrap.Height);
                        var uv1 = new Vector2((944f + 96f) / checkWrap.Width, (96f + 96f) / checkWrap.Height);

                        var size = 32f; 
                        var iconPos = max - new Vector2(size + 6f, size + 6f);
                        dl.AddImage(checkWrap.Handle, iconPos, iconPos + new Vector2(size, size), uv0, uv1);
                    }
                    else
                    {
                        var size = ImGui.CalcTextSize("\uF00C");
                        var iconPos = max - new Vector2(size.X + 8f, size.Y + 8f);
                        ImGui.SetCursorScreenPos(iconPos);
                        ImGui.TextColored(Theme.AccentSuccess, "\uF00C");
                    }
                }
                else
                {
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    var dl = ImGui.GetWindowDrawList();

                    var texIncomplete = Plugin.TextureProvider.GetFromGame("ui/icon/061000/061552_hr1.tex");
                    if (texIncomplete.TryGetWrap(out var incWrap, out _))
                    {
                        var size = 32f;
                        var center = (min + max) * 0.5f;
                        var iconPos = center - new Vector2(size * 0.5f, size * 0.5f);
                        dl.AddImage(incWrap.Handle, iconPos, iconPos + new Vector2(size, size),
                            Vector2.Zero, Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.7f)));
                    }
                    else
                    {
                        var text = "INCOMPLETE";
                        var statusColor = Theme.TextDisabled;
                        var textWidth = ImGui.CalcTextSize(text).X;
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetContentRegionAvail().X - textWidth) * 0.5f);
                        ImGui.TextColored(statusColor, text);
                    }
                }
                return;
            }
        }

        var bgCol = isDone ? Theme.AccentSuccess with { W = 0.3f } : Theme.BgCard;
        ImGui.PushStyleColor(ImGuiCol.Button, bgCol);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgCol);
        ImGui.Button($"{name}\n{(isDone ? "CLEARED" : "INCOMPLETE")}", new Vector2(-1, 80));
        ImGui.PopStyleColor(2);
    }

    internal static bool IsCofferRelevant(string cofferName, int slotId)
    {
        if (slotId == 0 && cofferName.Contains("Weapon")) return true;
        if (slotId == 2 && cofferName.Contains("Head")) return true;
        if (slotId == 3 && cofferName.Contains("Body")) return true;
        if (slotId == 4 && cofferName.Contains("Hand")) return true;
        if (slotId == 6 && cofferName.Contains("Leg")) return true;
        if (slotId == 7 && cofferName.Contains("Foot")) return true;
        if ((slotId >= 8 && slotId <= 12) && cofferName.Contains("Accessory")) return true;
        return false;
    }
}
