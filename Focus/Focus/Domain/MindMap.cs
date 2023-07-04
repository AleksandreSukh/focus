using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Infrastructure;

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

        public MindMap(Node node)
        {
            node.Number = 1;
            RootNode = node;
            _currentNode = RootNode;
        }

        public Node RootNode { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void SaveTo(string filePath)
        {
            File.WriteAllText(filePath, this.ToString());
        }

        public void AddAtCurrentNode(string input) => _currentNode.Add(input);
        
        public void AddIdeaAtCurrentNode(string input) => _currentNode.Add(input, NodeType.IdeaBagItem);

        public void LoadAtCurrentNode(MindMap anotherMap) => _currentNode.Add(anotherMap.RootNode);

        public bool ChangeCurrentNode(string nodeIdentifier)
        {
            var newNode = FindNode(nodeIdentifier);
            if (newNode == null) return false;
            var parentNode = _currentNode;
            _currentNode = newNode;
            _currentNode.SetParent(parentNode);
            return true;
        }

        private Node FindNode(string parameter)
        {
            var currentNodes = _currentNode.Children;

            if (int.TryParse(parameter, out int nodeNumber))
            {
                var targetNode = currentNodes.FirstOrDefault(n => n.Number == nodeNumber);
                return targetNode;
            }

            var shortcutNumber = AccessibleKeyNumbering.GetNumberFor(parameter);
            if (shortcutNumber != 0)
            {
                var targetNode = currentNodes.FirstOrDefault(n => n.Number == shortcutNumber);
                if (targetNode != null)
                    return targetNode;
            }

            return currentNodes.FirstOrDefault(n =>
                n.Name.StartsWith(parameter, StringComparison.InvariantCultureIgnoreCase));
        }

        //TODO:Refactor double usages of FindNode
        public bool HasNode(string identifier) => FindNode(identifier) != null;

        public bool GoToRoot()
        {
            _currentNode = RootNode;
            return true;
        }

        public bool GoUp()
        {
            var parentNode = _currentNode.GetParent();
            if (parentNode == null) return false;
            _currentNode = parentNode;
            return true;
        }

        public string GetCurrentSubtreeString()
        {
            var sb = new StringBuilder();
            _currentNode.Print("* ", false, 0, sb, ConsoleWrapper.WindowWidth - 5);
            return sb.ToString();
        }

        public string GetCurrentNodeName() => _currentNode.Name;

        public Dictionary<int, string> GetChildren()
        {
            RenumberChildNodes(); //TODO: only necessary after deletion. remove unnecessary call after 
            return _currentNode.Children.ToDictionary(n => n.Number, n => n.Name);
        }

        public void EditCurrentNode(string newString)
        {
            _currentNode.Name = newString;
        }

        public bool DeleteNode(string nodeIdentifier)
        {
            var nodeToDelete = FindNode(nodeIdentifier);
            if (nodeToDelete == null) return false;

            var removeResult = _currentNode.Children.Remove(nodeToDelete);
            RenumberChildNodes();
            return removeResult;
        }

        public bool DeleteCurrentNode()
        {
            var nodeToDelete = _currentNode.Number.ToString();
            return GoUp() && DeleteNode(nodeToDelete);
        }

        private void RenumberChildNodes()
        {
            var childNodes = _currentNode.Children.OrderBy(cn => cn.Number).ToArray();
            for (int i = 0; i < childNodes.Count(); i++)
            {
                childNodes[i].Number = i + 1;
            }
        }

        public MindMap DetachNode(string nodeIdentifier)
        {
            var nodeToDetach = FindNode(nodeIdentifier);
            if (nodeToDetach == null) return null;

            DeleteNode(nodeIdentifier); //TODO: suboptimal way of deleting subnode
            return new MindMap(nodeToDetach);
        }

        public bool HideNode(string nodeIdentifier)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null) return false;

            node.Collapsed = true;
            return true;
        }

        public bool UnhideNode(string nodeIdentifier)
        {
            var node = FindNode(nodeIdentifier);
            if (node == null) return false;
            node.Collapsed = false;
            return true;
        }

        public bool IsAtRootNode()
        {
            return _currentNode == RootNode;
        }
    }
}