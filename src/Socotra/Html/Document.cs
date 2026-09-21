// Description: Html Agility Pack - HTML Parsers, selectors, traversors, manupulators.
// Website & Documentation: http://html-agility-pack.net
// Forum & Issues: https://github.com/zzzprojects/html-agility-pack
// License: https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2017. All rights reserved.

#nullable disable

namespace Socotra.Html;

internal class Document
{
    private int _c;
    private Attribute _currentattribute;
    private Node _currentnode;
    private Node _documentnode;
    private bool _fullcomment;
    private int _index;
    internal Dictionary<string, Node> Lastnodes = new Dictionary<string, Node>();
    private Node _lastparentnode;
    internal Dictionary<int, Node> Openednodes;
    private ParseState _state;

    public string Text;

    public Node DocumentNode => _documentnode;

    public void LoadHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        Openednodes = new Dictionary<int, Node>();
        Text = html;
        _documentnode = CreateNode(NodeType.Document, 0);
        Parse();
        Openednodes.Clear();
    }

    internal Node CreateNode(NodeType type, int index) => type == NodeType.Text ? new TextNode(this, index) : new Node(type, this, index);

    internal void UpdateLastParentNode()
    {
        do
        {
            if (_lastparentnode.Closed)
            {
                _lastparentnode = _lastparentnode.ParentNode;
            }
        } while ((_lastparentnode != null) && (_lastparentnode.Closed));

        if (_lastparentnode == null)
        {
            _lastparentnode = _documentnode;
        }
    }

    private static bool IsWhiteSpace(int c) => c is 10 or 13 or 32 or 9;

    private void CloseCurrentNode()
    {
        if (_currentnode.Closed)
        {
            return;
        }

        Node prev = Lastnodes.GetValueOrDefault(_currentnode.Name);

        if (prev == null)
        {
            throw new System.Exception("Couldn't find previous tag");
        }

        Lastnodes[_currentnode.Name] = prev._prevwithsamename;
        prev.CloseNode(_currentnode);

        if (_lastparentnode != null)
        {
            UpdateLastParentNode();
        }
    }

    private bool IsValidTag()
    {
        bool isValidTag = _c == '<' && _index < Text.Length && (Char.IsLetter(Text[_index]) || Text[_index] == '/' || Text[_index] == '?' || Text[_index] == '!' || Text[_index] == '%');
        return isValidTag;
    }

    private bool NewCheck()
    {
        if (_c != '<' || !IsValidTag())
        {
            return false;
        }

        if (!PushNodeEnd(_index - 1, true))
        {
            _index = Text.Length;
            return true;
        }

        _state = ParseState.WhichTag;
        if ((_index - 1) <= (Text.Length - 2))
        {
            if (Text[_index] == '!' || Text[_index] == '?')
            {
                PushNodeStart(NodeType.Comment, _index - 1);
                PushNodeNameStart(true, _index);
                PushNodeNameEnd(_index + 1);
                _state = ParseState.Comment;
                if (_index < (Text.Length - 2))
                {
                    if ((Text[_index + 1] == '-') &&
                        (Text[_index + 2] == '-'))
                    {
                        _fullcomment = true;
                    }
                    else
                    {
                        _fullcomment = false;
                    }
                }

                return true;
            }
        }

        PushNodeStart(NodeType.Element, _index - 1);
        return true;
    }

    private void Parse()
    {
        int lastquote = 0;

        Lastnodes = new Dictionary<string, Node>();
        _c = 0;
        _fullcomment = false;

        _state = ParseState.Text;
        _documentnode._innerlength = Text.Length;
        _documentnode._outerlength = Text.Length;

        _lastparentnode = _documentnode;
        _currentnode = CreateNode(NodeType.Text, 0);
        _currentattribute = null;

        _index = 0;
        PushNodeStart(NodeType.Text, 0);
        while (_index < Text.Length)
        {
            _c = Text[_index];
            _index++;

            switch (_state)
            {
                case ParseState.Text:
                    if (NewCheck())
                    {
                        continue;
                    }

                    break;

                case ParseState.WhichTag:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (_c == '/')
                    {
                        PushNodeNameStart(false, _index);
                    }
                    else
                    {
                        PushNodeNameStart(true, _index - 1);
                        _index--;
                    }

                    _state = ParseState.Tag;
                    break;

                case ParseState.Tag:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        PushNodeNameEnd(_index - 1);
                        if (_state != ParseState.Tag)
                        {
                            continue;
                        }

                        _state = ParseState.BetweenAttributes;
                        continue;
                    }

                    if (_c == '/')
                    {
                        PushNodeNameEnd(_index - 1);
                        if (_state != ParseState.Tag)
                        {
                            continue;
                        }

                        _state = ParseState.EmptyTag;
                        continue;
                    }

                    if (_c == '>')
                    {
                        PushNodeNameEnd(_index - 1);
                        if (_state != ParseState.Tag)
                        {
                            continue;
                        }

                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.Tag)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                    }

                    break;

                case ParseState.BetweenAttributes:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        continue;
                    }

                    if ((_c == '/') || (_c == '?'))
                    {
                        _state = ParseState.EmptyTag;
                        continue;
                    }

                    if (_c == '>')
                    {
                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.BetweenAttributes)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    PushAttributeNameStart(_index - 1);
                    _state = ParseState.AttributeName;
                    break;

                case ParseState.EmptyTag:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (_c == '>')
                    {
                        if (!PushNodeEnd(_index, true))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.EmptyTag)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    if (!IsWhiteSpace(_c))
                    {
                        _index--;
                        _state = ParseState.BetweenAttributes;
                        continue;
                    }
                    else
                    {
                        _state = ParseState.BetweenAttributes;
                    }

                    break;

                case ParseState.AttributeName:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        PushAttributeNameEnd(_index - 1);
                        _state = ParseState.AttributeBeforeEquals;
                        continue;
                    }

                    if (_c == '=')
                    {
                        PushAttributeNameEnd(_index - 1);
                        _state = ParseState.AttributeAfterEquals;
                        continue;
                    }

                    if (_c == '>')
                    {
                        PushAttributeNameEnd(_index - 1);
                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.AttributeName)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    break;

                case ParseState.AttributeBeforeEquals:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        continue;
                    }

                    if (_c == '>')
                    {
                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.AttributeBeforeEquals)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    if (_c == '=')
                    {
                        _state = ParseState.AttributeAfterEquals;
                        continue;
                    }

                    _state = ParseState.BetweenAttributes;
                    _index--;
                    break;

                case ParseState.AttributeAfterEquals:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        continue;
                    }

                    if ((_c == '\'') || (_c == '"'))
                    {
                        _state = ParseState.QuotedAttributeValue;
                        PushAttributeValueStart(_index);
                        lastquote = _c;
                        continue;
                    }

                    if (_c == '>')
                    {
                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.AttributeAfterEquals)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    PushAttributeValueStart(_index - 1);
                    _state = ParseState.AttributeValue;
                    break;

                case ParseState.AttributeValue:
                    if (NewCheck())
                    {
                        continue;
                    }

                    if (IsWhiteSpace(_c))
                    {
                        PushAttributeValueEnd(_index - 1);
                        _state = ParseState.BetweenAttributes;
                        continue;
                    }

                    if (_c == '>')
                    {
                        PushAttributeValueEnd(_index - 1);
                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        if (_state != ParseState.AttributeValue)
                        {
                            continue;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    break;

                case ParseState.QuotedAttributeValue:
                    if (_c == lastquote)
                    {
                        PushAttributeValueEnd(_index - 1);
                        _state = ParseState.BetweenAttributes;
                        continue;
                    }

                    break;

                case ParseState.Comment:
                    if (_c == '>')
                    {
                        if (_fullcomment)
                        {
                            if (((Text[_index - 2] != '-') || (Text[_index - 3] != '-'))
                                &&
                                ((Text[_index - 2] != '!') || (Text[_index - 3] != '-') ||
                                 (Text[_index - 4] != '-')))
                            {
                                continue;
                            }
                        }

                        if (!PushNodeEnd(_index, false))
                        {
                            _index = Text.Length;
                            break;
                        }

                        _state = ParseState.Text;
                        PushNodeStart(NodeType.Text, _index);
                        continue;
                    }

                    break;
            }
        }

        if (_currentnode._namestartindex > 0)
        {
            PushNodeNameEnd(_index);
        }

        PushNodeEnd(_index, false);

        Lastnodes.Clear();

        DocumentNode.FixSelfClosingTags();
    }

    private void PushAttributeNameEnd(int index)
    {
        _currentattribute._namelength = index - _currentattribute._namestartindex;

        if (_currentattribute.Name is not ("\"" or "'"))
        {
            _currentnode.Attributes.Add(_currentattribute);
        }
    }

    private void PushAttributeNameStart(int index)
    {
        _currentattribute = new Attribute(this);
        _currentattribute._namestartindex = index;
    }

    private void PushAttributeValueEnd(int index)
    {
        _currentattribute._valuelength = index - _currentattribute._valuestartindex;
    }

    private void PushAttributeValueStart(int index)
    {
        _currentattribute._valuestartindex = index;
    }

    private bool PushNodeEnd(int index, bool close)
    {
        _currentnode._outerlength = index - _currentnode._outerstartindex;

        if ((_currentnode._nodetype == NodeType.Text) || (_currentnode._nodetype == NodeType.Comment))
        {
            if (_currentnode._nodetype != NodeType.Comment && _currentnode._outerlength > 0)
            {
                _currentnode._innerlength = _currentnode._outerlength;
                _currentnode._innerstartindex = _currentnode._outerstartindex;
                if (_lastparentnode != null)
                {
                    _lastparentnode.AppendChild(_currentnode);
                }
            }
        }
        else
        {
            if ((_currentnode._starttag) && (_lastparentnode != _currentnode))
            {
                if (_lastparentnode != null)
                {
                    _lastparentnode.AppendChild(_currentnode);
                }

                Node prev = Lastnodes.GetValueOrDefault(_currentnode.Name);

                _currentnode._prevwithsamename = prev;
                Lastnodes[_currentnode.Name] = _currentnode;

                if ((_currentnode.NodeType == NodeType.Document) ||
                    (_currentnode.NodeType == NodeType.Element))
                {
                    _lastparentnode = _currentnode;
                }
            }
        }

        if (_currentnode.Name == "img" || _currentnode.Name == "br" || _currentnode.Name == "video")
        {
            close = true;
        }

        if ((close) || (!_currentnode._starttag))
        {
            CloseCurrentNode();
        }

        return true;
    }

    private void PushNodeNameEnd(int index)
    {
        _currentnode._namelength = index - _currentnode._namestartindex;
    }

    private void PushNodeNameStart(bool starttag, int index)
    {
        _currentnode._starttag = starttag;
        _currentnode._namestartindex = index;
    }

    private void PushNodeStart(NodeType type, int index)
    {
        _currentnode = CreateNode(type, index);
    }

    private enum ParseState
    {
        Text,
        WhichTag,
        Tag,
        BetweenAttributes,
        EmptyTag,
        AttributeName,
        AttributeBeforeEquals,
        AttributeAfterEquals,
        AttributeValue,
        Comment,
        QuotedAttributeValue,
    }
}
