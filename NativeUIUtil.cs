﻿using System;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WheresWOLdo
{
    /***
     * This was lifted directly from https://github.com/lmcintyre/OrchestrionPlugin/blob/main/Orchestrion/NativeUIUtil.cs
     * This code is MIT Licensed, which can be found on https://github.com/lmcintyre/OrchestrionPlugin/blob/main/LICENSE
     */
    public unsafe class NativeUIUtil
    {
        // "PBOR" in ASCII
        private const int NodeId = 0x50424F52;
        private readonly Configuration configuration;
        private readonly GameGui gameGui;

        internal NativeUIUtil(Configuration configuration, GameGui gameGui)
        {
            this.configuration = configuration;
            this.gameGui = gameGui;
            if (!FFXIVClientStructs.Resolver.Initialized)
                FFXIVClientStructs.Resolver.Initialize();
        }

        private AtkUnitBase* GetDTR()
        {
            return (AtkUnitBase*)gameGui.GetAddonByName("_DTR", 1).ToPointer();
        }

        private AtkTextNode* GetTextNode()
        {
            var dtr = GetDTR();
            if (dtr == null) return null;
            for (int i = 0; i < dtr->UldManager.NodeListCount; i++)
            {
                var node = dtr->UldManager.NodeList[i];
                if (node->NodeID == NodeId) return (AtkTextNode*)node;
            }

            return null;
        }

        public void Init()
        {
            // try
            // {
            var dtr = GetDTR();
            if (dtr == null || dtr->UldManager.NodeListCount < 16 || dtr->UldManager.SearchNodeById(NodeId) != null) return;
            PluginLog.Debug($"DTR @ {(ulong)dtr:X}");

            PluginLog.Debug("Finding last sibling node to add to DTR");
            if (dtr->RootNode == null) return;
            var lastChild = dtr->RootNode->ChildNode;

            // Create text node for jello world
            PluginLog.Debug("Creating our text node.");
            var locationNode = CreateTextNode();
            PluginLog.Debug("Text node created.");

            while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;
            PluginLog.Debug($"Found last sibling: {(ulong)lastChild:X}");
            lastChild->PrevSiblingNode = (AtkResNode*)locationNode;
            locationNode->AtkResNode.ParentNode = lastChild->ParentNode;
            locationNode->AtkResNode.NextSiblingNode = lastChild;

            dtr->RootNode->ChildCount = (ushort)(dtr->RootNode->ChildCount + 1);
            PluginLog.Debug("Set last sibling of DTR and updated child count");

            dtr->UldManager.UpdateDrawNodeList();
            PluginLog.Debug("Updated node draw list");
            // }
            // catch (Exception ignored)
            // {
            //     // ignored
            // }
        }

        public void Update(string text = null)
        {
            // We obtain DTR first to prevent ourselves from attempting to
            // initialize even when DTR is not visible
            var dtr = GetDTR();
            var musicNode = GetTextNode();

            if (dtr != null && musicNode == null && configuration.ShowLocationInNative)
                Init();

            if (dtr == null || musicNode == null) return;
            var collisionNode = dtr->UldManager.NodeList[1];
            var xPos = collisionNode->Width;
            musicNode->AtkResNode.SetPositionFloat(xPos * -1f + musicNode->AtkResNode.Width, 2);

            // TODO: WIP text truncation
            // if (text != null)
            // {
            //     var len = text.Length;
            //     fixed (byte* textPtr = Encoding.UTF8.GetBytes(text))
            //     {
            //         ushort w = ushort.MaxValue, h = 0;
            //         while (w > 120)
            //             musicNode->GetTextDrawSize(&w, &h, textPtr, 0, len--);
            //     }
            //     var settableText = text;
            //     musicNode->SetText($"{text}");
            // }

            if (text != null)
                musicNode->SetText(text);
        }

        private AtkTextNode* CreateTextNode()
        {
            var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if (newTextNode == null)
            {
                PluginLog.Debug("Failed to allocate memory for text node");
                return null;
            }
            IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
            newTextNode->Ctor();

            newTextNode->AtkResNode.NodeID = NodeId;
            newTextNode->AtkResNode.Type = NodeType.Text;
            newTextNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
            newTextNode->AtkResNode.DrawFlags = 12;
            newTextNode->AtkResNode.SetWidth(120);
            newTextNode->AtkResNode.SetHeight(22);
            newTextNode->AtkResNode.SetPositionFloat(-200, 2);

            newTextNode->LineSpacing = 12;
            newTextNode->AlignmentFontType = 5;
            newTextNode->FontSize = 14;
            newTextNode->TextFlags = (byte)(TextFlags.Edge);
            newTextNode->TextFlags2 = 0;

            newTextNode->SetText("♪ ");

            newTextNode->TextColor.R = 255;
            newTextNode->TextColor.G = 255;
            newTextNode->TextColor.B = 255;
            newTextNode->TextColor.A = 255;

            newTextNode->EdgeColor.R = 142;
            newTextNode->EdgeColor.G = 106;
            newTextNode->EdgeColor.B = 12;
            newTextNode->EdgeColor.A = 255;

            return newTextNode;
        }

        private void DisposeTextNode()
        {
            PluginLog.Debug("Disposing text node.");
            var musicNode = GetTextNode();
            if (musicNode == null) return;
            musicNode->AtkResNode.Destroy(true);
        }

        public void Dispose()
        {
            var dtr = GetDTR();
            var musicNode = GetTextNode();
            if (dtr == null || musicNode == null) return;

            PluginLog.Debug("Unlinking Text node...");
            var relNode = musicNode->AtkResNode.NextSiblingNode;
            relNode->PrevSiblingNode = null;
            DisposeTextNode();
            PluginLog.Debug("Decrementing dtr->RootNode->ChildCount by 1...");
            dtr->RootNode->ChildCount = (ushort)(dtr->RootNode->ChildCount - 1);
            PluginLog.Debug("Calling UpdateDrawNodeList()...");
            dtr->UldManager.UpdateDrawNodeList();
            PluginLog.Debug("Dispose done!");
        }
    }
}