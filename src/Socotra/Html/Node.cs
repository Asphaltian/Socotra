// Description: Html Agility Pack - HTML Parsers, selectors, traversors, manupulators.
// Website & Documentation: http://html-agility-pack.net
// Forum & Issues: https://github.com/zzzprojects/html-agility-pack
// License: https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2017. All rights reserved.

#nullable disable

namespace Socotra.Html;

/// <summary>An element or piece of text in a rich text label's markup.</summary>
public interface INode : IStyleTarget
{
    /// <summary>Whether it's an element, like <c>&lt;b&gt;</c>.</summary>
    bool IsElement { get; }

    /// <summary>Whether it's text.</summary>
    bool IsText { get; }

    /// <summary>Whether it's a comment.</summary>
    bool IsComment { get; }

    /// <summary>Whether it's the document holding everything else.</summary>
    bool IsDocument { get; }

    /// <summary>The node's markup, including its own tag.</summary>
    string OuterHtml { get; }

    /// <summary>The markup inside the node.</summary>
    string InnerHtml { get; }

    /// <summary>The nodes directly inside this one.</summary>
    new IEnumerable<INode> Children { get; }

    /// <summary>The element's tag name, like <c>a</c>.</summary>
    string Name { get; }

    /// <summary>An attribute's value, or <paramref name="def"/> if the element doesn't have it.</summary>
    string GetAttribute(string name, string def = "");

    internal void SetPseudoClass(PseudoClass c);

    /// <summary>Reads <paramref name="html"/> into a tree of nodes. You get the document node back, with the top level nodes as its <see cref="Children"/>.</summary>
    /// <example><code>
    /// // Walk the elements of some rich text
    /// var document = INode.Parse("Press &lt;b&gt;Start&lt;/b&gt; to begin");
    /// foreach (var node in document.Children.Where(n => n.IsElement))
    /// {
    ///     Console.WriteLine(node.Name);
    /// }
    /// </code></example>
    public static INode Parse(string html) => Node.Parse(html);
}

internal class Node : INode
{
    internal List<Attribute> _attributes;
    internal List<Node> _childnodes;
    internal Node _endnode;
    internal string _innerhtml;
    internal int _innerlength;
    internal int _innerstartindex;
    internal int _namelength;
    internal int _namestartindex;
    internal NodeType _nodetype;
    internal string _outerhtml;
    internal int _outerlength;
    internal int _outerstartindex;
    internal Document _ownerdocument;
    internal Node _prevwithsamename;
    internal bool _starttag;
    private string _optimizedName;
    private PseudoClass _ps;

    internal Node(NodeType type, Document ownerdocument, int index)
    {
        _nodetype = type;
        _ownerdocument = ownerdocument;
        _outerstartindex = index;

        switch (type)
        {
            case NodeType.Comment:
            case NodeType.Text:
                _endnode = this;
                break;

            case NodeType.Document:
                _optimizedName = "#document";
                _endnode = this;
                break;
        }

        if (!Closed && index != -1)
        {
            _ownerdocument.Openednodes.Add(index, this);
        }
    }

    public bool IsElement => NodeType == NodeType.Element;

    public bool IsComment => NodeType == NodeType.Comment;

    public bool IsText => NodeType == NodeType.Text;

    public bool IsDocument => NodeType == NodeType.Document;

    public List<Attribute> Attributes
    {
        get
        {
            if (!HasAttributes)
            {
                _attributes = new List<Attribute>();
            }

            return _attributes;
        }
    }

    public List<Node> ChildNodes => _childnodes ??= new List<Node>();

    public IEnumerable<INode> Children => _childnodes ?? Enumerable.Empty<INode>();

    public bool HasAttributes => _attributes != null && _attributes.Count > 0;

    public bool HasChildNodes => _childnodes != null && _childnodes.Count > 0;

    public virtual string InnerHtml => _innerhtml ??= _ownerdocument.Text.Substring(_innerstartindex, _innerlength);

    public string Name => _optimizedName ??= _ownerdocument.Text.Substring(_namestartindex, _namelength).ToLowerInvariant();

    public virtual string OuterHtml => _outerhtml ??= _ownerdocument.Text.Substring(_outerstartindex, _outerlength);

    public Node ParentNode { get; private set; }

    internal bool Closed => _endnode != null;

    internal NodeType NodeType => _nodetype;

    string IStyleTarget.ElementName => Name;

    string IStyleTarget.Id => GetAttribute("id");

    PseudoClass IStyleTarget.PseudoClass => _ps;

    IStyleTarget IStyleTarget.Parent => ParentNode;

    int IStyleTarget.SiblingIndex => 0;

    IReadOnlyList<IStyleTarget> IStyleTarget.Children => _childnodes?.AsReadOnly();

    public static Node Parse(string html)
    {
        var document = new Document();
        document.LoadHtml(html);
        return document.DocumentNode;
    }

    void INode.SetPseudoClass(PseudoClass c) => _ps = c;

    bool IStyleTarget.HasClasses(string[] classes)
    {
        var all = GetAttribute("class").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return classes.All(name => all.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    public void AppendChild(Node newChild)
    {
        ChildNodes.Add(newChild);
        newChild.ParentNode = this;
    }

    public string GetAttribute(string name, string def = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!HasAttributes)
        {
            return def;
        }

        return Attributes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? def;
    }

    internal void UpdateLastNode()
    {
        Node newLast = null;
        if (_prevwithsamename == null || !_prevwithsamename._starttag)
        {
            foreach (var openNode in _ownerdocument.Openednodes)
            {
                if ((openNode.Key < _outerstartindex || openNode.Key > (_outerstartindex + _outerlength)) && openNode.Value.Name == Name)
                {
                    if (newLast == null && openNode.Value._starttag)
                    {
                        newLast = openNode.Value;
                    }
                    else if (newLast != null && newLast._innerstartindex < openNode.Key && openNode.Value._starttag)
                    {
                        newLast = openNode.Value;
                    }
                }
            }
        }
        else
        {
            newLast = _prevwithsamename;
        }

        if (newLast != null)
        {
            _ownerdocument.Lastnodes[newLast.Name] = newLast;
        }
    }

    internal void CloseNode(Node endnode)
    {
        if (Closed)
        {
            return;
        }

        _endnode = endnode;
        _ownerdocument.Openednodes.Remove(_outerstartindex);

        Node self = _ownerdocument.Lastnodes.GetValueOrDefault(Name);
        if (self == this)
        {
            _ownerdocument.Lastnodes.Remove(Name);
            _ownerdocument.UpdateLastParentNode();

            if (_starttag && !String.IsNullOrEmpty(Name))
            {
                UpdateLastNode();
            }
        }

        if (endnode == this)
        {
            return;
        }

        _innerstartindex = _outerstartindex + _outerlength;
        _innerlength = endnode._outerstartindex - _innerstartindex;
        _outerlength = (endnode._outerstartindex + endnode._outerlength) - _outerstartindex;
    }

    internal void FixSelfClosingTags()
    {
        if (!HasChildNodes)
        {
            return;
        }

        foreach (var child in ChildNodes.ToArray())
        {
            child.FixSelfClosingTags();

            if (child.Closed)
            {
                continue;
            }

            var index = ChildNodes.IndexOf(child);

            foreach (var gchild in child.ChildNodes)
            {
                ChildNodes.Insert(++index, gchild);
            }

            child.ChildNodes.Clear();
        }
    }
}
