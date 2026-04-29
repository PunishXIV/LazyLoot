using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace LazyLoot;

internal static class Utils
{
    private static readonly Dictionary<string, object> States = new();

    public static unsafe int GetPlayerIlevel()
    {
        return UIState.Instance()->CurrentItemLevel;
    }


    public static bool CheckboxTextWrapped(string text, ref bool v)
    {
        ImGui.PushID(text);

        var changed = ImGui.Checkbox("##chk", ref v);
        ImGui.SameLine();

        var wrapEndX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.PushTextWrapPos(wrapEndX);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();

        if (ImGui.IsItemClicked())
        {
            v = !v;
            changed = true;
        }

        ImGui.PopID();
        return changed;
    }

    private static PopupListState<T> GetState<T>(string popupId)
    {
        if (States.TryGetValue(popupId, out var obj) && obj is PopupListState<T> typed)
            return typed;

        var created = new PopupListState<T>();
        States[popupId] = created;
        return created;
    }

    public static void PopupListButton<T>(
        string buttonLabel,
        string popupId,
        string popupTitle,
        Func<string, IEnumerable<T>> getResults,
        Func<T, string> getItemLabel,
        Action<T> onSelect,
        Action<T>? renderItem = null,
        Func<T, string?>? getTooltip = null,
        float minWidth = 300f,
        Vector2? listSize = null,
        int maxResults = 100,
        float debounceSeconds = 0.1f,
        int inputMaxLength = 100)
    {
        var state = GetState<T>(popupId);

        if (ImGui.Button(buttonLabel))
        {
            state.FocusOnOpen = true;
            ImGui.OpenPopup(popupId);
        }

        if (!ImGui.BeginPopup(popupId))
            return;

        if (!string.IsNullOrEmpty(popupTitle))
        {
            ImGuiEx.TextCentered(popupTitle);
            ImGui.Dummy(new Vector2(0, 4));
        }

        var now = ImGui.GetTime();
        var shouldRefresh =
            now > state.LastSearchTime + debounceSeconds;

        if (shouldRefresh)
        {
            state.LastSearchTime = now;

            var q = state.Query.Trim();
            var results = getResults(q);

            state.CachedResults = results
                .Take(maxResults)
                .ToList();
        }

        var widest = state.CachedResults.Count == 0
            ? 200f
            : state.CachedResults
                .Select(x => ImGui.CalcTextSize(getItemLabel(x)).X)
                .DefaultIfEmpty(200f)
                .Max();

        var width = MathF.Max(minWidth, widest + 30f);
        ImGui.SetNextItemWidth(width);

        if (state.FocusOnOpen)
        {
            ImGui.SetKeyboardFocusHere();
            state.FocusOnOpen = false;
        }
        
        ImGui.InputText("##popupListSearch", ref state.Query, inputMaxLength);
        ImGui.Dummy(new Vector2(0, 4));

        var childSize = listSize ?? new Vector2(width, 200);
        try
        {
            var style = ImGui.GetStyle();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { Y = 6f });
            if (ImGui.BeginChild("##popupListResults", childSize, true))
            {
                foreach (var item in state.CachedResults)
                {
                    renderItem?.Invoke(item);

                    if (getTooltip != null && ImGui.IsItemHovered())
                    {
                        var tip = getTooltip(item);
                        if (!string.IsNullOrEmpty(tip))
                            ImGui.SetTooltip(tip);
                    }

                    var label = getItemLabel(item);
                    if (!ImGui.Selectable(label)) continue;
                    onSelect(item);
                    ImGui.CloseCurrentPopup();
                    break;
                }

                ImGui.EndChild();
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }

        ImGui.EndPopup();
    }

    private sealed class PopupListState<T>
    {
        public List<T> CachedResults = [];
        public bool FocusOnOpen;
        public double LastSearchTime;
        public string Query = "";
    }
}