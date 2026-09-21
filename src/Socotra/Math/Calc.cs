namespace Socotra;

internal abstract class Calc
{
    private string _source;

    private Calc(string source)
    {
        _source = source;
    }

    public static bool IsExpression(string value) =>
        value.StartsWith("calc(", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("min(", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("max(", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("clamp(", StringComparison.OrdinalIgnoreCase);

    public static Calc? Parse(string value)
    {
        var reader = new Reader(value);
        try
        {
            var expression = reader.ReadSum();
            if (!reader.IsEnd)
            {
                return null;
            }

            expression._source = value;
            return expression;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public abstract float Evaluate(float dimension);

    public override string ToString() => _source;

    private sealed class Literal(Length value, string source) : Calc(source)
    {
        public override float Evaluate(float dimension) => value.GetScaledPixels(dimension);
    }

    private sealed class Number(float value, string source) : Calc(source)
    {
        public override float Evaluate(float dimension) => value;
    }

    private sealed class Operation(char op, Calc left, Calc right, string source) : Calc(source)
    {
        public override float Evaluate(float dimension) => op switch
        {
            '+' => left.Evaluate(dimension) + right.Evaluate(dimension),
            '-' => left.Evaluate(dimension) - right.Evaluate(dimension),
            '*' => left.Evaluate(dimension) * right.Evaluate(dimension),
            _ => left.Evaluate(dimension) / right.Evaluate(dimension),
        };
    }

    private sealed class Function(string name, Calc[] arguments, string source) : Calc(source)
    {
        public override float Evaluate(float dimension)
        {
            if (name == "clamp")
            {
                return MathF.Max(arguments[0].Evaluate(dimension), MathF.Min(arguments[1].Evaluate(dimension), arguments[2].Evaluate(dimension)));
            }

            float result = arguments[0].Evaluate(dimension);
            for (int i = 1; i < arguments.Length; i++)
            {
                var value = arguments[i].Evaluate(dimension);
                result = name == "min" ? MathF.Min(result, value) : MathF.Max(result, value);
            }

            return result;
        }
    }

    private ref struct Reader(string text)
    {
        private int _position;

        public readonly bool IsEnd => SkipSpaces(_position) >= text.Length;

        public Calc ReadSum()
        {
            int start = SkipSpaces(_position);
            var left = ReadProduct();
            while (TryOperator('+', '-') is { } op)
            {
                left = new Operation(op, left, ReadProduct(), text[start.._position].Trim());
            }

            return left;
        }

        private Calc ReadProduct()
        {
            int start = SkipSpaces(_position);
            var left = ReadValue();
            while (TryOperator('*', '/') is { } op)
            {
                left = new Operation(op, left, ReadValue(), text[start.._position].Trim());
            }

            return left;
        }

        private Calc ReadValue()
        {
            _position = SkipSpaces(_position);
            int start = _position;
            if (TrySkip("("))
            {
                var inner = ReadSum();
                Expect(')');
                return inner;
            }

            foreach (var name in (ReadOnlySpan<string>)["calc", "min", "max", "clamp"])
            {
                if (!TrySkip(name + "("))
                {
                    continue;
                }

                var arguments = new List<Calc> { ReadSum() };
                while (TrySkip(","))
                {
                    arguments.Add(ReadSum());
                }

                Expect(')');
                if (name == "calc")
                {
                    return arguments.Count == 1 ? arguments[0] : throw new FormatException();
                }

                if (name == "clamp" && arguments.Count != 3)
                {
                    throw new FormatException();
                }

                return new Function(name, [.. arguments], text[start.._position]);
            }

            int end = _position;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not (',' or ')' or '(' or '*' or '/'))
            {
                end++;
            }

            var token = text[_position..end];
            _position = end;
            switch (token.ToLowerInvariant())
            {
                case "pi":
                    return new Number(MathF.PI, token);
                case "e":
                    return new Number(MathF.E, token);
            }

            if (Translation.TryParseFloat(token, out var number))
            {
                return new Number(number, token);
            }

            return Length.Parse(token) is { Unit: not LengthUnit.Expression } length ? new Literal(length, token) : throw new FormatException();
        }

        private char? TryOperator(char a, char b)
        {
            int position = SkipSpaces(_position);
            if (position >= text.Length || (text[position] != a && text[position] != b))
            {
                return null;
            }

            _position = position + 1;
            return text[position];
        }

        private bool TrySkip(string value)
        {
            int position = SkipSpaces(_position);
            if (string.Compare(text, position, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            _position = position + value.Length;
            return true;
        }

        private void Expect(char c)
        {
            if (!TrySkip(c.ToString()))
            {
                throw new FormatException();
            }
        }

        private readonly int SkipSpaces(int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }

            return position;
        }
    }
}
