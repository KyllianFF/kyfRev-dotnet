namespace KyfRev.Rendering;

/// <summary>Un passage approché : sa position et le nombre de différences avec le texte cherché.</summary>
/// <param name="Start">Début.</param>
/// <param name="End">Fin.</param>
/// <param name="Errors">Nombre de différences.</param>
internal readonly record struct ApproximateHit(int Start, int End, int Errors);

/// <summary>
/// Recherche approchée par l'algorithme de Myers, bit à bit : les passages les moins différents du texte
/// cherché. Reprend <c>approx-string-match</c>, celle de la 0.x : le candidat proposé en dépend.
/// </summary>
internal static class ApproximateMatch
{
    private const int Word = 32;

    public static List<ApproximateHit> Search(string text, string pattern, int maxErrors)
    {
        var reversedPattern = Reversed(pattern);
        var hits = new List<ApproximateHit>();
        foreach (var match in FindEnds(text, pattern, maxErrors))
        {
            var minimumStart = Math.Max(0, match.End - pattern.Length - match.Errors);
            var reversedText = Reversed(text[minimumStart..match.End]);
            var start = match.End;
            foreach (var reversed in FindEnds(reversedText, reversedPattern, match.Errors))
            {
                if (match.End - reversed.End < start)
                {
                    start = match.End - reversed.End;
                }
            }
            hits.Add(new ApproximateHit(start, match.End, match.Errors));
        }
        return hits;
    }

    private static string Reversed(string text)
    {
        var characters = text.ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }

    private static int OneIfNotZero(uint value) => value != 0 ? 1 : 0;

    private static int AdvanceBlock(uint[] plus, uint[] minus, uint[] lastRowMask, uint[] peq, int block, int carryIn)
    {
        unchecked
        {
            var pV = plus[block];
            var mV = minus[block];
            var negative = carryIn < 0 ? 1u : 0u;
            var eq = peq[block] | negative;
            var xV = eq | mV;
            var xH = ((eq & pV) + pV) ^ pV | eq;
            var pH = mV | ~(xH | pV);
            var mH = pV & xH;
            var carryOut = OneIfNotZero(pH & lastRowMask[block]) - OneIfNotZero(mH & lastRowMask[block]);
            pH <<= 1;
            mH <<= 1;
            mH |= negative;
            pH |= carryIn > 0 ? 1u : 0u;
            plus[block] = mH | ~(xV | pH);
            minus[block] = pH & xV;
            return carryOut;
        }
    }

    private static List<ApproximateHit> FindEnds(string text, string pattern, int maxErrors)
    {
        var matches = new List<ApproximateHit>();
        var allowed = Math.Min(maxErrors, pattern.Length);
        var lastBlock = ((pattern.Length + Word - 1) / Word) - 1;
        var plus = new uint[lastBlock + 1];
        var minus = new uint[lastBlock + 1];
        var lastRowMask = new uint[lastBlock + 1];
        Array.Fill(lastRowMask, 1u << 31);
        lastRowMask[lastBlock] = 1u << ((pattern.Length - 1) % Word);
        var empty = new uint[lastBlock + 1];
        var masks = new Dictionary<char, uint[]>();
        for (var i = 0; i < pattern.Length; i++)
        {
            if (!masks.TryGetValue(pattern[i], out var mask))
            {
                mask = new uint[lastBlock + 1];
                masks[pattern[i]] = mask;
            }
            mask[i / Word] |= 1u << (i % Word);
        }

        var top = Math.Max(0, ((allowed + Word - 1) / Word) - 1);
        var score = new int[lastBlock + 1];
        for (var block = 0; block <= top; block++)
        {
            score[block] = (block + 1) * Word;
        }
        score[lastBlock] = pattern.Length;
        for (var block = 0; block <= top; block++)
        {
            plus[block] = uint.MaxValue;
            minus[block] = 0;
        }
        for (var j = 0; j < text.Length; j++)
        {
            var peq = masks.GetValueOrDefault(text[j], empty);
            var carry = 0;
            for (var block = 0; block <= top; block++)
            {
                carry = AdvanceBlock(plus, minus, lastRowMask, peq, block, carry);
                score[block] += carry;
            }
            if (score[top] - carry <= allowed && top < lastBlock && ((peq[top + 1] & 1) != 0 || carry < 0))
            {
                top++;
                plus[top] = uint.MaxValue;
                minus[top] = 0;
                var remainder = pattern.Length % Word;
                var maxBlockScore = top == lastBlock && remainder != 0 ? remainder : Word;
                score[top] = score[top - 1] + maxBlockScore - carry + AdvanceBlock(plus, minus, lastRowMask, peq, top, carry);
            }
            else
            {
                while (top > 0 && score[top] >= allowed + Word)
                {
                    top--;
                }
            }
            if (top == lastBlock && score[top] <= allowed)
            {
                if (score[top] < allowed)
                {
                    matches.Clear();
                }
                matches.Add(new ApproximateHit(-1, j + 1, score[top]));
                allowed = score[top];
            }
        }
        return matches;
    }
}
