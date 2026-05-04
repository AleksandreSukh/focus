#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Domain
{
    [Serializable]
    public class MindMap
    {
        private enum NodeLookupScope
        {
            VisibleSelectable,
            TaskAddressable
        }

        private Node _currentNode;

        public MindMap()
        {
            RootNode = new Node();
            _currentNode = RootNode;
        }

        public MindMap(string name) : this(new Node(name, NodeType.TextItem, 1)) { }

        public MindMap(Node nodeToCopyFrom)
        {
            var node = JsonConvert.DeserializeObject<Node>(
                JsonConvert.SerializeObject(nodeToCopyFrom, JsonSerialization.CreateDefaultSettings()),
                JsonSerialization.CreateDefaultSettings())!;
            node.Number = 1;
            RootNode = node;
            _currentNode = RootNode;
        }

        public Node RootNode { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, JsonSerialization.CreateDefaultSettings());
        }

        public Node AddAtCurrentNode(
            string input,
            string source = NodeMetadataSources.Manual,
            string? device = null)
        {
            var node = _currentNode.Add(input, NodeType.TextItem, source, device);
            TouchMapTimestamp();
            return node;
        }

        public Node AddIdeaAtCurrentNode(
            string input,
            string source = NodeMetadataSources.Manual,
            string? device = null)
        {
            var node = _currentNode.Add(input, NodeType.IdeaBagItem, source, device);
            TouchMapTimestamp();
            return node;
        }

        public Node AddBlockAtCurrentNode(
            string input,
            string source = NodeMetadataSources.Manual,
            string? device = null)
        {
            var node = _currentNode.Add(input, NodeType.TextBlockItem, source, device);
            TouchMapTimestamp();
            return node;
        }

        public void EditCurrentNode(string newString)
        {
            _currentNode.EditNode(newString);
            TouchMapTimestamp();
        }

        public Node LoadAtCurrentNode(MindMap anotherMap)
        {
            var node = _currentNode.Add(anotherMap.RootNode);
            TouchMapTimestamp();
            return node;
        }

        public void LinkToCurrentNode(
            Node linkedNode,
            LinkRelationType relationType = LinkRelationType.Relates,
            string? metadata = null)
        {
            _currentNode.AddLink(linkedNode, relationType, metadata);
            TouchMapTimestamp();
        }

        public bool LinkToNode(
            string nodeIdentifier,
            Node nodeToLinkFrom,
            LinkRelationType relationType = LinkRelationType.Relates,
            string? metadata = null)
        {
            var nodeToLinkTo = FindNode(nodeIdentifier);
            if (nodeToLinkTo == null)
                return false;

            nodeToLinkFrom.AddLink(nodeToLinkTo, relationType, metadata);
            TouchMapTimestamp();
            return true;
        }

        public bool ChangeCurrentNode(string nodeIdentifier)
        {
            var newNode = FindNode(nodeIdentifier);
            if (newNode == null)
                return false;

            var parentNode = _currentNode;
            _currentNode = newNode;
            _currentNode.SetParent(parentNode);
            return true;
        }

        public bool ChangeCurrentNodeById(Guid nodeIdentifier)
        {
            var node = FindNodeById(RootNode, nodeIdentifier);
            if (node == null)
                return false;

            _currentNode = node;
            return true;
        }

        public bool DeleteChildNode(string nodeIdentifier)
        {
            var nodeToDelete = FindNode(nodeIdentifier);
            if (nodeToDelete == null)
                return false;

            var result = DeleteChildNode(_currentNode, nodeToDelete);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool DeleteCurrentNodeIdeaTags()
        {
            var result = ClearIdeaTagsOfNode(_currentNode);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool DeleteNodeIdeaTags(string nodeIdentifier)
        {
            var nodeToClear = FindNode(nodeIdentifier);
            if (nodeToClear == null) return false;
            var result = ClearIdeaTagsOfNode(nodeToClear);
            if (result) TouchMapTimestamp();
            return result;
        }

        public MindMap? DetachCurrentNodeAsNewMap()
        {
            var nodeToDetach = _currentNode;
            var parentNode = nodeToDetach.GetParent();
            var detachedMap = DetachNodeAsNewMap(nodeToDetach);
            if (detachedMap != null && parentNode != null)
            {
                _currentNode = parentNode;
                TouchMapTimestamp();
            }

            return detachedMap;
        }

        public MindMap? DetachNodeAsNewMap(string nodeIdentifier)
        {
            var nodeToDetach = FindNode(nodeIdentifier);
            return nodeToDetach == null
                ? null
                : DetachNodeAsNewMap(nodeToDetach);
        }

        public Dictionary<int, string> GetChildren()
        {
            _currentNode.RenumberChildNodes();
            return GetVisibleSelectableChildren(_currentNode)
                .ToDictionary(node => node.Number, node => NodeDisplayHelper.GetSingleLinePreview(node.Name));
        }

        public Node GetCurrentNode() => _currentNode;

        public string GetCurrentNodeContentPeek() => NodeDisplayHelper.GetContentPeek(_currentNode.Name);

        public Guid? GetCurrentNodeIdentifier() => _currentNode.UniqueIdentifier;

        public string GetCurrentNodeName() => _currentNode.Name;

        public Node? GetNode(string identifier) => FindNode(identifier);

        public string GetNodeContentPeekByIdentifier(string identifier)
        {
            var node = FindNode(identifier);
            return node == null
                ? string.Empty
                : NodeDisplayHelper.GetContentPeek(node.Name);
        }

        public bool GoToRoot()
        {
            _currentNode = RootNode;
            return true;
        }

        public bool GoUp()
        {
            var parentNode = _currentNode.GetParent();
            if (parentNode == null)
                return false;

            _currentNode = parentNode;
            return true;
        }

        public bool HasNode(string identifier) => FindNode(identifier) != null;

        public bool HideNode(string nodeIdentifier)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
                return false;

            node.Collapse();
            TouchMapTimestamp();
            return true;
        }

        public bool StarNode(out string errorMessage)
        {
            var result = SetStarred(_currentNode, starred: true, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool StarNode(string nodeIdentifier, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            var result = SetStarred(node, starred: true, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool UnstarNode(out string errorMessage)
        {
            var result = SetStarred(_currentNode, starred: false, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool UnstarNode(string nodeIdentifier, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            var result = SetStarred(node, starred: false, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool SetHideDoneTasks(bool hideDoneTasks, out string errorMessage)
        {
            var result = SetHideDoneTasks(_currentNode, hideDoneTasks, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool SetHideDoneTasks(string nodeIdentifier, bool hideDoneTasks, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier, NodeLookupScope.TaskAddressable);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            var result = SetHideDoneTasks(node, hideDoneTasks, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool IsAtRootNode() => _currentNode == RootNode;

        public bool SetTaskState(TaskState taskState, out string errorMessage)
        {
            var result = SetTaskState(_currentNode, taskState, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool SetTaskState(string nodeIdentifier, TaskState taskState, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier, NodeLookupScope.TaskAddressable);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            var result = SetTaskState(node, taskState, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool ToggleTaskState(out string errorMessage)
        {
            var result = ToggleTaskState(_currentNode, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public bool ToggleTaskState(string nodeIdentifier, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier, NodeLookupScope.TaskAddressable);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            var result = ToggleTaskState(node, out errorMessage);
            if (result) TouchMapTimestamp();
            return result;
        }

        public void ResetCurrentNodeToRoot()
        {
            _currentNode = RootNode;
        }

        public bool UnhideNode(string nodeIdentifier)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
                return false;

            node.Expand();
            TouchMapTimestamp();
            return true;
        }

        public bool ScrubDeadLinks(ISet<Guid> liveNodeIds)
        {
            var changed = ScrubNodeLinks(RootNode, liveNodeIds);
            if (changed)
                TouchMapTimestamp();
            return changed;
        }

        private static bool ScrubNodeLinks(Node node, ISet<Guid> liveNodeIds)
        {
            var changed = node.RemoveDeadLinks(liveNodeIds);
            foreach (var child in node.Children)
                changed |= ScrubNodeLinks(child, liveNodeIds);
            return changed;
        }

        private void TouchMapTimestamp() => UpdatedAt = DateTimeOffset.UtcNow;

        private bool ClearIdeaTagsOfNode(Node nodeToClear)
        {
            var ideaTagsToRemove = nodeToClear.Children.Where(node => node.NodeType == NodeType.IdeaBagItem).ToArray();
            if (!ideaTagsToRemove.Any())
                return false;

            foreach (var ideaTag in ideaTagsToRemove)
            {
                nodeToClear.Children.Remove(ideaTag);
            }

            nodeToClear.RenumberChildNodes();
            nodeToClear.TouchMetadata();
            return true;
        }

        private static bool DeleteChildNode(Node? parentNode, Node nodeToDelete)
        {
            if (parentNode == null)
                return false;

            var removeResult = parentNode.Children.Remove(nodeToDelete);
            parentNode.RenumberChildNodes();
            if (removeResult)
                parentNode.TouchMetadata();
            return removeResult;
        }

        private MindMap? DetachNodeAsNewMap(Node nodeToDetach)
        {
            var nodeToDetachFrom = nodeToDetach.GetParent();
            if (nodeToDetachFrom == null)
                return null;

            var detachedMap = new MindMap(nodeToDetach);
            return DeleteChildNode(nodeToDetachFrom, nodeToDetach)
                ? detachedMap
                : null;
        }

        private static bool SetTaskState(Node node, TaskState taskState, out string errorMessage)
        {
            if (node.GetParent() == null)
            {
                errorMessage = "Can't change task state for root node";
                return false;
            }

            if (node.NodeType == NodeType.IdeaBagItem)
            {
                errorMessage = "Task mode is not supported for idea tags";
                return false;
            }

            node.TaskState = taskState;
            node.TouchMetadata();
            errorMessage = string.Empty;
            return true;
        }

        private static bool ToggleTaskState(Node node, out string errorMessage) =>
            SetTaskState(node, node.TaskState.Toggle(), out errorMessage);

        private static bool SetStarred(Node node, bool starred, out string errorMessage)
        {
            var parent = node.GetParent();
            if (parent == null)
            {
                errorMessage = "Can't change starred state for root node";
                return false;
            }

            if (node.NodeType == NodeType.IdeaBagItem)
            {
                errorMessage = "Starred state is not supported for idea tags";
                return false;
            }

            node.Starred = starred;
            ReorderStarredChild(parent, node, starred);
            parent.RenumberChildNodes();
            node.TouchMetadata();
            parent.TouchMetadata();
            errorMessage = string.Empty;
            return true;
        }

        private static void ReorderStarredChild(Node parent, Node node, bool starred)
        {
            if (!parent.Children.Remove(node))
                return;

            var insertIndex = starred
                ? GetFirstSelectableChildIndex(parent)
                : GetUnstarredInsertionIndex(parent);
            parent.Children.Insert(insertIndex, node);
        }

        private static int GetFirstSelectableChildIndex(Node parent)
        {
            var index = parent.Children.FindIndex(child => child.NodeType != NodeType.IdeaBagItem);
            return index >= 0 ? index : parent.Children.Count;
        }

        private static int GetUnstarredInsertionIndex(Node parent)
        {
            var lastStarredIndex = parent.Children.FindLastIndex(child =>
                child.NodeType != NodeType.IdeaBagItem && child.Starred);
            if (lastStarredIndex >= 0)
                return lastStarredIndex + 1;

            return GetFirstSelectableChildIndex(parent);
        }

        private static bool SetHideDoneTasks(Node node, bool hideDoneTasks, out string errorMessage)
        {
            if (node.NodeType == NodeType.IdeaBagItem)
            {
                errorMessage = "Hide done tasks is not supported for idea tags";
                return false;
            }

            node.SetHideDoneTasks(hideDoneTasks);
            ClearDescendantHideDoneOverrides(node);
            errorMessage = string.Empty;
            return true;
        }

        private static void ClearDescendantHideDoneOverrides(Node node)
        {
            foreach (var child in node.Children)
            {
                child.ClearHideDoneTasksOverride();
                ClearDescendantHideDoneOverrides(child);
            }
        }

        private Node? FindNode(string parameter, NodeLookupScope lookupScope = NodeLookupScope.VisibleSelectable)
        {
            var visibleNode = FindNodeInCandidates(parameter, GetVisibleSelectableChildren(_currentNode));
            if (visibleNode != null || lookupScope == NodeLookupScope.VisibleSelectable)
                return visibleNode;

            return FindNodeInCandidates(
                parameter,
                _currentNode.Children.Where(node => node.NodeType == NodeType.IdeaBagItem));
        }

        private static Node? FindNodeInCandidates(string parameter, IEnumerable<Node> candidates)
        {
            var candidateNodes = candidates.ToArray();

            if (int.TryParse(parameter, out var nodeNumber))
            {
                return candidateNodes.FirstOrDefault(node => node.Number == nodeNumber);
            }

            var shortcutNumber = Infrastructure.Input.AccessibleKeyNumbering.GetNumberFor(parameter);
            if (shortcutNumber != 0)
                return candidateNodes.FirstOrDefault(node => node.Number == shortcutNumber);

            return candidateNodes.FirstOrDefault(node =>
                NodeDisplayHelper.GetSingleLinePreview(node.Name)
                    .StartsWith(parameter, StringComparison.InvariantCultureIgnoreCase));
        }

        private static IEnumerable<Node> GetVisibleSelectableChildren(Node node) =>
            NodeExportHelpers.GetVisibleChildren(node, NodeBranchVisibility.HideDoneStateForNode(node));

        private static Node? FindNodeById(Node currentNode, Guid nodeIdentifier)
        {
            if (currentNode.UniqueIdentifier == nodeIdentifier)
                return currentNode;

            foreach (var childNode in currentNode.Children)
            {
                var matchingNode = FindNodeById(childNode, nodeIdentifier);
                if (matchingNode != null)
                    return matchingNode;
            }

            return null;
        }
    }
}
