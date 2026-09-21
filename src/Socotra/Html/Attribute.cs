// Description: Html Agility Pack - HTML Parsers, selectors, traversors, manupulators.
// Website & Documentation: http://html-agility-pack.net
// Forum & Issues: https://github.com/zzzprojects/html-agility-pack
// License: https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2017. All rights reserved.

using System.Diagnostics;
using System.Web;

#nullable disable

namespace Socotra.Html;

[DebuggerDisplay("Name: {Name}, Value: {Value}")]
internal class Attribute
{
    internal string _name;
    internal int _namelength;
    internal int _namestartindex;
    internal string _value;
    internal int _valuelength;
    internal int _valuestartindex;

    private readonly string _text;

    internal Attribute(Document ownerdocument)
    {
        _text = ownerdocument.Text;
    }

    public string Name => _name ??= _text.Substring(_namestartindex, _namelength).ToLowerInvariant();

    public string Value => _value ??= GetValue();

    private string GetValue()
    {
        if (_valuestartindex <= 0)
        {
            return string.Empty;
        }

        if (_valuelength <= 0)
        {
            return string.Empty;
        }

        return HttpUtility.HtmlDecode(_text.Substring(_valuestartindex, _valuelength));
    }
}
