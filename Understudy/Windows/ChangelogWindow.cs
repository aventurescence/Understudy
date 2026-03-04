using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Understudy.Models;

namespace Understudy.Windows;

public class ChangelogWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string titleParams = string.Empty;
    private string markdownBody = string.Empty;
    private string releaseDate = DateTime.Now.ToString("MMMM d yyyy");
    private bool isFetching = true;
    private bool fetchFailed = false;
    private ISharedImmediateTexture? headerTexture;
    private ISharedImmediateTexture? avatarTexture;

    public ChangelogWindow(Plugin plugin) : base("Changelog", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        
        Size = new Vector2(500, 650);
        SizeCondition = ImGuiCond.Always;
        try
        {
            var headerPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty, "Assets", "Understudy3.png");
            if (File.Exists(headerPath))
            {
                headerTexture = Plugin.TextureProvider.GetFromFile(headerPath);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load header image.");
        }

        try
        {
            var avatarPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty, "Assets", "aventurescence.png");
            if (File.Exists(avatarPath))
            {
                avatarTexture = Plugin.TextureProvider.GetFromFile(avatarPath);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load aventurescence avatar texture");
        }

        Task.Run(FetchChangelog);
    }

    public void Dispose()
    {
        Plugin.PluginInterface.UiBuilder.Draw -= Draw;
    }

    private async Task FetchChangelog()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/aventurescence/Understudy/releases/latest");
            // GitHub API requires a user-agent
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Understudy", plugin.GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0"));
            
            var response = await plugin.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            
            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameProp))
            {
                titleParams = nameProp.GetString() ?? string.Empty;
            }
            if (root.TryGetProperty("body", out var bodyProp))
            {
                markdownBody = bodyProp.GetString() ?? string.Empty;
            }
            if (root.TryGetProperty("published_at", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(dateProp.GetString(), out var pubDate))
                {
                    releaseDate = pubDate.ToString("MMMM d yyyy");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to fetch changelog from GitHub.");
            fetchFailed = true;
        }
        finally
        {
            isFetching = false;
        }
    }

    public override void Draw()
    {
        if (isFetching)
        {
            ImGui.Text("Loading changelog...");
            return;
        }

        if (fetchFailed)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, "Failed to load changelog.");
            if (ImGui.Button("Close"))
            {
                IsOpen = false;
            }
            return;
        }

        var availWidth = ImGui.GetContentRegionAvail().X;
        var bottomHeight = 60f * ImGui.GetIO().FontGlobalScale;
        
        var cursorStart = ImGui.GetCursorScreenPos();
        float imgHeight = 0;
        
        // Draw the header image if available
        if (headerTexture != null && headerTexture.TryGetWrap(out var headerWrap, out _))
        {
            var drawList = ImGui.GetWindowDrawList();
            
            float targetHeight = 220f * 1.15f * ImGui.GetIO().FontGlobalScale; // Larger banner
            float targetWidth = availWidth;
            float imgW = headerWrap.Width;
            float imgH = headerWrap.Height;

            // Fill entire header space (object-fit: cover)
            float scale = Math.Max(targetWidth / imgW, targetHeight / imgH);
            
            float visibleW = targetWidth / scale;
            float visibleH = targetHeight / scale;

            // Anchor bottom left:
            Vector2 uvMin = new Vector2(0f, (imgH - visibleH) / imgH);
            Vector2 uvMax = new Vector2(visibleW / imgW, 1f);

            imgHeight = targetHeight;
            
            var pMin = cursorStart;
            var pMax = pMin + new Vector2(targetWidth, targetHeight);
            
            // Draw the base image
            drawList.AddImage(headerWrap.Handle, pMin, pMax, uvMin, uvMax);
            
            // Add a gradient overlay to fade it to 0% opacity near the top (to blend with background)
            uint colTop = ImGui.GetColorU32(Theme.BgDark); // Solid window background color at top
            uint colBottom = ImGui.GetColorU32(new Vector4(Theme.BgDark.X, Theme.BgDark.Y, Theme.BgDark.Z, 0f)); // Transparent at bottom
            
            // Fade the top 50% of the banner
            var fadeMax = new Vector2(pMax.X, pMin.Y + targetHeight * 0.5f);
            drawList.AddRectFilledMultiColor(pMin, fadeMax, colTop, colTop, colBottom, colBottom);
            
            // Advance cursor past the image, then reset it to draw text on top
            ImGui.SetCursorScreenPos(cursorStart + new Vector2(20f, 30f) * ImGui.GetIO().FontGlobalScale); // Padding for text
        }
        
        ImGui.SetWindowFontScale(1.3f);
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(Theme.AccentSuccess, FontAwesomeIcon.Star.ToIconString());
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextColored(Theme.AccentSuccess, "What's New");
        ImGui.SetWindowFontScale(1f);
        
        ImGui.SameLine();
        
        var versionStr = plugin.GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var subText = $"Understudy v{versionStr} — {titleParams}";
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 0.3f * ImGui.GetFontSize());
        ImGui.TextColored(Theme.TextSecondary, subText);

        if (headerTexture != null && imgHeight > 0)
        {
             ImGui.SetCursorScreenPos(cursorStart + new Vector2(0, imgHeight + 10f * ImGui.GetIO().FontGlobalScale));
        }
        else
        {
             ImGuiHelpers.ScaledDummy(10f);
        }

        var lineDrawList = ImGui.GetWindowDrawList();
        var lineCursor = ImGui.GetCursorScreenPos();
        lineDrawList.AddLine(lineCursor, lineCursor + new Vector2(availWidth, 0), ImGui.GetColorU32(Theme.AccentPrimary), 2.0f);
        ImGuiHelpers.ScaledDummy(10f);

        ImGui.PushStyleColor(ImGuiCol.Tab, Theme.BgCard);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, Theme.BgCardHover);
        ImGui.PushStyleColor(ImGuiCol.TabActive, Theme.AccentPrimary);
        
        if (ImGui.BeginTabBar("ChangelogTabs"))
        {
            if (ImGui.BeginTabItem("Changelog"))
            {
                if (ImGui.BeginChild("ChangelogContent", new Vector2(0, ImGui.GetContentRegionAvail().Y - bottomHeight), true))
                {
                    string headerLabel = $"v{versionStr} — {releaseDate} — {titleParams}";
                    ImGui.PushStyleColor(ImGuiCol.Header, Theme.BgCard);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Theme.BgCardHover);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, Theme.BgCard);
                    ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentSuccess);
                    bool isOpen = ImGui.CollapsingHeader(headerLabel, ImGuiTreeNodeFlags.DefaultOpen);
                    ImGui.PopStyleColor(4);
                    if (isOpen)
                    {
                        RenderMarkdown(markdownBody);
                    }
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Credits"))
            {
                if (ImGui.BeginChild("CreditsContent", new Vector2(0, ImGui.GetContentRegionAvail().Y - bottomHeight), true))
                {
                    float creditsAvailW = ImGui.GetContentRegionAvail().X;
                    var dl = ImGui.GetWindowDrawList();
                    ImGuiHelpers.ScaledDummy(20f);
                    float centerX = ImGui.GetCursorPosX() + creditsAvailW * 0.5f;

                    float globalScale = ImGui.GetIO().FontGlobalScale;
                    float avatarSize = 100f * globalScale;
                    float pillHeight = ImGui.GetTextLineHeight() + 10f * globalScale;
                    float cardWidth = 440f * globalScale;
                    if (cardWidth > creditsAvailW - 16f * globalScale)
                        cardWidth = creditsAvailW - 16f * globalScale;
                        
                    float cardHeight = 330f * globalScale;
                    float creditsAvailH = ImGui.GetContentRegionAvail().Y;
                    if (cardHeight > creditsAvailH - 16f * globalScale)
                        cardHeight = creditsAvailH - 16f * globalScale;
                    
                    ImGui.SetCursorPosX(centerX - cardWidth * 0.5f);
                    var cardStart = ImGui.GetCursorScreenPos();
                    var cardEnd = cardStart + new Vector2(cardWidth, cardHeight);
                    
                    dl.AddRectFilled(cardStart, cardEnd, ImGui.GetColorU32(Theme.BgCard), Theme.CardRounding);
                    dl.AddRect(cardStart, cardEnd, ImGui.GetColorU32(Theme.BorderCard), Theme.CardRounding);

                    string nameText = "Aventurescence";
                    var nameTextSize = ImGui.CalcTextSize(nameText);
                    var namePillSize = nameTextSize + new Vector2(24f * globalScale, 10f * globalScale);
                    
                    ImGui.PushFont(UiBuilder.IconFont);
                    string githubIcon = FontAwesomeIcon.Link.ToIconString();
                    var iconSize = ImGui.CalcTextSize(githubIcon);
                    ImGui.PopFont();
                    
                    string linkText = " github.com/aventurescence";
                    var linkTextSize = ImGui.CalcTextSize(linkText);
                    var githubPillSize = new Vector2(iconSize.X + linkTextSize.X + 24f * globalScale, Math.Max(iconSize.Y, linkTextSize.Y) + 10f * globalScale);

                    float contentHeight = avatarSize + (10f * globalScale) + namePillSize.Y + (8f * globalScale) + githubPillSize.Y + (16f * globalScale); // Added bottom padding to the block
                    float topPadding = (cardHeight - contentHeight) * 0.5f;

                    // Apply top padding
                    if (topPadding > 0)
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + topPadding);
                    }

                    // Draw Avatar
                    if (avatarTexture != null && avatarTexture.TryGetWrap(out var avatarWrap, out _))
                    {
                        ImGui.SetCursorPosX(centerX - avatarSize * 0.5f);
                        var pAvatarMin = ImGui.GetCursorScreenPos();
                        var pAvatarMax = pAvatarMin + new Vector2(avatarSize);
                        dl.AddImageRounded(avatarWrap.Handle, pAvatarMin, pAvatarMax, Vector2.Zero, Vector2.One, ImGui.GetColorU32(Vector4.One), avatarSize * 0.5f);
                        
                        // Clickable avatar
                        ImGui.InvisibleButton("##avatarLink", new Vector2(avatarSize));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                            if (ImGui.IsItemClicked())
                                Util.OpenLink("https://github.com/aventurescence");
                        }
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(avatarSize));
                    }

                    ImGuiHelpers.ScaledDummy(10f);

                    // Name Pill
                    ImGui.SetCursorPosX(centerX - namePillSize.X * 0.5f);
                    
                    var pNameMin = ImGui.GetCursorScreenPos();
                    var pNameMax = pNameMin + namePillSize;
                    
                    ImGui.InvisibleButton("##namePill", namePillSize);
                    bool nameHovered = ImGui.IsItemHovered();
                    dl.AddRectFilled(pNameMin, pNameMax, ImGui.GetColorU32(nameHovered ? Theme.BgCardHover : Theme.BgSidebar), 12f * globalScale);
                    
                    var nameTextPos = pNameMin + new Vector2(12f * globalScale, 5f * globalScale);
                    dl.AddText(nameTextPos, ImGui.GetColorU32(nameHovered ? Vector4.One : Theme.AccentSuccess), nameText);

                    ImGuiHelpers.ScaledDummy(8f);

                    // Github Link Pill Underneath
                    ImGui.SetCursorPosX(centerX - githubPillSize.X * 0.5f);
                    
                    var pLinkMin = ImGui.GetCursorScreenPos();
                    var pLinkMax = pLinkMin + githubPillSize;
                    
                    ImGui.InvisibleButton("##githubPill", githubPillSize);
                    bool linkHovered = ImGui.IsItemHovered();
                    if (linkHovered)
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        if (ImGui.IsItemClicked())
                            Util.OpenLink("https://github.com/aventurescence");
                    }
                    
                    dl.AddRectFilled(pLinkMin, pLinkMax, ImGui.GetColorU32(linkHovered ? Theme.TrackBtnHover : Theme.TrackBtnBg), 12f * globalScale);
                    
                    var iconPos = pLinkMin + new Vector2(12f * globalScale, 5f * globalScale);
                    ImGui.PushFont(UiBuilder.IconFont);
                    dl.AddText(iconPos, ImGui.GetColorU32(Vector4.One), githubIcon);
                    ImGui.PopFont();
                    
                    var linkPos = iconPos + new Vector2(iconSize.X, 0);
                    dl.AddText(linkPos, ImGui.GetColorU32(Vector4.One), linkText);
                    
                    ImGui.SetCursorScreenPos(cardEnd); // advance past card
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.PopStyleColor(3);

        ImGuiHelpers.ScaledDummy(10f);

        var buttonWidth = 200f * ImGui.GetIO().FontGlobalScale;
        var buttonHeight = 40f * ImGui.GetIO().FontGlobalScale;
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - buttonWidth) * 0.5f);
        
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentSecondary);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1,1,1,1));
        
        if (ImGui.Button("Got it!", new Vector2(buttonWidth, buttonHeight)))
        {
            IsOpen = false;
        }
        
        ImGui.PopStyleColor(3);
    }
    
    private void RenderMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            string trimLine = line.TrimEnd();
            if (string.IsNullOrEmpty(trimLine))
            {
                ImGui.Spacing();
                continue;
            }

            if (trimLine.StartsWith("### ") || trimLine.StartsWith("## "))
            {
                string headerText = trimLine.StartsWith("### ") ? trimLine.Substring(4) : trimLine.Substring(3);
                ImGuiHelpers.ScaledDummy(10f);
                
                var availWidth = ImGui.GetContentRegionAvail().X;
                var pMin = ImGui.GetCursorScreenPos();
                var pMax = new Vector2(pMin.X + availWidth, pMin.Y + ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2);
                
                ImGui.GetWindowDrawList().AddRectFilled(pMin, pMax, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 1f)));
                
                ImGui.SetCursorScreenPos(pMin + new Vector2(8f, ImGui.GetStyle().FramePadding.Y));
                ImGui.TextColored(Theme.AccentSuccess, headerText);
                
                ImGui.SetCursorScreenPos(new Vector2(pMin.X, pMax.Y + 4f));
            }
            else if (trimLine.StartsWith("- ") || trimLine.StartsWith("* "))
            {
                ImGui.Bullet();
                ImGui.TextWrapped(trimLine.Substring(2));
            }
            else
            {
                ImGui.TextWrapped(trimLine);
            }
        }
    }
}
