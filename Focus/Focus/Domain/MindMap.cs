#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Pages.Shared.DialogHelpers;

namespace Systems.Sanity.Focus.Domain
{
    [Serializable]
    public class MindMap
    {
        private Node _currentNode;

        public MindMap()
        {
            RootNode = new Node();
            _currentNode = RootNode;
        }

        public MindMap(string name) : this(new Node(name, NodeType.TextItem, 1)) { }

        public MindMap(Node nodeToCopyFrom)
        {
            var node = JsonConvert.DeserializeObject<Node>(JsonConvert.SerializeObject(nodeToCopyFrom))!;
            node.Number = 1;
            RootNode = node;
            _currentNode = RootNode;
        }

        public Node RootNode { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void AddAtCurrentNode(string input) => _currentNode.Add(input);

        public void AddIdeaAtCurrentNode(string input) => _currentNode.Add(input, NodeType.IdeaBagItem);

        public void EditCurrentNode(string newString) => _currentNode.EditNode(newString);

        public void LoadAtCurrentNode(MindMap anotherMap) => _currentNode.Add(anotherMap.RootNode);

        public void LinkToCurrentNode(
            Node linkedNode,
            LinkRelationType relationType = LinkRelationType.Relates,
            string? metadata = null) => _currentNode.AddLink(linkedNode, relationType, metadata);

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

            return DeleteChildNode(_currentNode, nodeToDelete);
        }

        public bool DeleteCurrentNodeIdeaTags() => ClearIdeaTagsOfNode(_currentNode);

        public bool DeleteNodeIdeaTags(string nodeIdentifier)
        {
            var nodeToClear = FindNode(nodeIdentifier);
            return nodeToClear != null && ClearIdeaTagsOfNode(nodeToClear);
        }

        public MindMap? DetachCurrentNodeAsNewMap()
        {
            var nodeToDetach = _currentNode;
            var parentNode = nodeToDetach.GetParent();
            var detachedMap = DetachNodeAsNewMap(nodeToDetach);
            if (detachedMap != null && parentNode != null)
            {
                _currentNode = parentNode;
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
            return _currentNode.Children.ToDictionary(node => node.Number, node => node.Name);
        }

        public Node GetCurrentNode() => _currentNode;

        public string GetCurrentNodeContentPeek() => _currentNode.Name.GetContentPeek();

        public Guid? GetCurrentNodeIdentifier() => _currentNode.UniqueIdentifier;

        public string GetCurrentNodeName() => _currentNode.Name;

        public Node? GetNode(string identifier) => FindNode(identifier);

        public string GetNodeContentPeekByIdentifier(string identifier)
        {
            var node = FindNode(identifier);
            return node?.Name.GetContentPeek() ?? string.Empty;
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
            return true;
        }

        public bool IsAtRootNode() => _currentNode == RootNode;

        public bool SetTaskState(TaskState taskState, out string errorMessage) =>
            SetTaskState(_currentNode, taskState, out errorMessage);

        public bool SetTaskState(string nodeIdentifier, TaskState taskState, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            return SetTaskState(node, taskState, out errorMessage);
        }

        public bool ToggleTaskState(out string errorMessage) =>
            ToggleTaskState(_currentNode, out errorMessage);

        public bool ToggleTaskState(string nodeIdentifier, out string errorMessage)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null)
            {
                errorMessage = $"Can't find \"{nodeIdentifier}\"";
                return false;
            }

            return ToggleTaskState(node, out errorMessage);
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
            return true;
        }

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
            return true;
        }

        private static bool DeleteChildNode(Node? parentNode, Node nodeToDelete)
        {
            if (parentNode == null)
                return false;

            var removeResult = parentNode.Children.Remove(nodeToDelete);
            parentNode.RenumberChildNodes();
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
            errorMessage = string.Empty;
            return true;
        }

        private static bool ToggleTaskState(Node node, out string errorMessage) =>
            SetTaskState(node, node.TaskState.Toggle(), out errorMessage);

        private Node? FindNode(string parameter)
        {
            var currentNodes = _currentNode.Children;

            if (int.TryParse(parameter, out var nodeNumber))
            {
                return currentNodes.FirstOrDefault(node => node.Number == nodeNumber);
            }

            var shortcutNumber = Infrastructure.Input.AccessibleKeyNumbering.GetNumberFor(parameter);
            if (shortcutNumber != 0)
            {
                var targetNode = currentNodes.FirstOrDefault(node => node.Number == shortcutNumber);
                if (targetNode != null)
                    return targetNode;
            }

            return currentNodes.FirstOrDefault(node =>
                node.Name.StartsWith(parameter, StringComparison.InvariantCultureIgnoreCase));
        }

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
