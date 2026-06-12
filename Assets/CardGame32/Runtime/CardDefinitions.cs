using System;
using System.Collections.Generic;

namespace CardGame32
{
    public enum CardSuit
    {
        Joker,
        Spades,
        Hearts,
        Diamonds,
        Clubs
    }

    public enum CardRank
    {
        Joker,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        Two
    }

    public enum CardColor
    {
        None,
        Red,
        Black
    }

    [Serializable]
    public struct CardDefinition
    {
        public CardSuit Suit;
        public CardRank Rank;
        public CardColor Color;
        public string DisplayName;
        public string ShortName;
        public int PointValue;

        public CardDefinition(CardSuit suit, CardRank rank, string displayName, string shortName, int pointValue)
        {
            Suit = suit;
            Rank = rank;
            DisplayName = displayName;
            ShortName = shortName;
            PointValue = pointValue;
            Color = suit == CardSuit.Hearts || suit == CardSuit.Diamonds
                ? CardColor.Red
                : suit == CardSuit.Spades || suit == CardSuit.Clubs
                    ? CardColor.Black
                    : CardColor.None;
        }

        public bool IsJoker
        {
            get { return Rank == CardRank.Joker; }
        }

        public bool IsPowerRank
        {
            get { return Rank == CardRank.Queen || Rank == CardRank.Two || Rank == CardRank.Eight || Rank == CardRank.Four; }
        }
    }

    public enum HandCategory
    {
        PointSum = 0,
        PowerMixed = 10,
        Special28 = 20,
        SpecialQ8 = 21,
        SpecialQ9 = 22,
        Bomb = 30,
        RedPowerBomb = 31,
        JokerThree = 40
    }

    public struct HandEvaluation : IComparable<HandEvaluation>
    {
        public HandCategory Category;
        public int Primary;
        public int Secondary;
        public int PointSum;
        public string Label;

        public int CompareTo(HandEvaluation other)
        {
            int category = Category.CompareTo(other.Category);
            if (category != 0)
            {
                return category;
            }

            int primary = Primary.CompareTo(other.Primary);
            if (primary != 0)
            {
                return primary;
            }

            int secondary = Secondary.CompareTo(other.Secondary);
            if (secondary != 0)
            {
                return secondary;
            }

            return PointSum.CompareTo(other.PointSum);
        }
    }

    public static class CardGameRules
    {
        public static readonly IReadOnlyList<CardDefinition> Deck = BuildDeck();

        public static List<CardDefinition> CreateShuffledDeck()
        {
            List<CardDefinition> cards = new List<CardDefinition>(Deck);
            Random random = new Random(Guid.NewGuid().GetHashCode());

            for (int i = cards.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                CardDefinition temp = cards[i];
                cards[i] = cards[swapIndex];
                cards[swapIndex] = temp;
            }

            return cards;
        }

        public static HandEvaluation Evaluate(CardDefinition a, CardDefinition b)
        {
            int sum = a.PointValue + b.PointValue;

            if (IsJokerThree(a, b))
            {
                return NewEvaluation(HandCategory.JokerThree, 100, 0, sum, "\u5927\u738b\u914d3");
            }

            if (IsBomb(a, b))
            {
                bool redPowerBomb = a.IsPowerRank && a.Color == CardColor.Red && b.Color == CardColor.Red;
                return NewEvaluation(redPowerBomb ? HandCategory.RedPowerBomb : HandCategory.Bomb, RankWeight(a.Rank), 0, sum, "\u70b8\u5f39");
            }

            HandCategory specialCategory;
            string specialLabel;
            if (TryGetSpecialCombo(a, b, out specialCategory, out specialLabel))
            {
                return NewEvaluation(specialCategory, 0, 0, sum, specialLabel);
            }

            CardDefinition powerCard;
            if (TryGetPowerCard(a, b, out powerCard))
            {
                int colorWeight = powerCard.Color == CardColor.Red ? 1 : 0;
                return NewEvaluation(HandCategory.PowerMixed, PowerRankWeight(powerCard.Rank), colorWeight, sum, "\u7279\u6b8a\u6563\u724c");
            }

            return NewEvaluation(HandCategory.PointSum, sum, HighestRankWeight(a, b), sum, "\u70b9\u6570");
        }

        public static string DescribeHand(CardDefinition a, CardDefinition b)
        {
            HandEvaluation evaluation = Evaluate(a, b);
            return string.Format("{0} + {1} = {2} ({3})", a.DisplayName, b.DisplayName, evaluation.PointSum, evaluation.Label);
        }

        private static IReadOnlyList<CardDefinition> BuildDeck()
        {
            return new List<CardDefinition>
            {
                Card(CardSuit.Joker, CardRank.Joker, "\u5927\u738b", "\u738b", 6),
                Card(CardSuit.Spades, CardRank.Three, "\u9ed1\u68433", "\u26603", 3),
                Card(CardSuit.Hearts, CardRank.Queen, "\u7ea2\u5fc3Q", "\u2665Q", 2),
                Card(CardSuit.Diamonds, CardRank.Queen, "\u65b9\u5757Q", "\u2666Q", 2),
                Card(CardSuit.Hearts, CardRank.Two, "\u7ea2\u5fc32", "\u26652", 2),
                Card(CardSuit.Diamonds, CardRank.Two, "\u65b9\u57572", "\u26662", 2),
                Card(CardSuit.Hearts, CardRank.Eight, "\u7ea2\u5fc38", "\u26658", 8),
                Card(CardSuit.Diamonds, CardRank.Eight, "\u65b9\u57578", "\u26668", 8),
                Card(CardSuit.Hearts, CardRank.Four, "\u7ea2\u5fc34", "\u26654", 4),
                Card(CardSuit.Diamonds, CardRank.Four, "\u65b9\u57574", "\u26664", 4),
                Card(CardSuit.Spades, CardRank.Four, "\u9ed1\u68434", "\u26604", 4),
                Card(CardSuit.Clubs, CardRank.Four, "\u6885\u82b14", "\u26634", 4),
                Card(CardSuit.Spades, CardRank.Six, "\u9ed1\u68436", "\u26606", 6),
                Card(CardSuit.Clubs, CardRank.Six, "\u6885\u82b16", "\u26636", 6),
                Card(CardSuit.Hearts, CardRank.Six, "\u7ea2\u5fc36", "\u26656", 6),
                Card(CardSuit.Diamonds, CardRank.Six, "\u65b9\u57576", "\u26666", 6),
                Card(CardSuit.Spades, CardRank.Ten, "\u9ed1\u684310", "\u266010", 10),
                Card(CardSuit.Clubs, CardRank.Ten, "\u6885\u82b110", "\u266310", 10),
                Card(CardSuit.Hearts, CardRank.Ten, "\u7ea2\u5fc310", "\u266510", 10),
                Card(CardSuit.Diamonds, CardRank.Ten, "\u65b9\u575710", "\u266610", 10),
                Card(CardSuit.Hearts, CardRank.Jack, "\u7ea2\u5fc3J", "\u2665J", 1),
                Card(CardSuit.Diamonds, CardRank.Jack, "\u65b9\u5757J", "\u2666J", 1),
                Card(CardSuit.Hearts, CardRank.Nine, "\u7ea2\u5fc39", "\u26659", 9),
                Card(CardSuit.Diamonds, CardRank.Nine, "\u65b9\u57579", "\u26669", 9),
                Card(CardSuit.Spades, CardRank.Seven, "\u9ed1\u68437", "\u26607", 7),
                Card(CardSuit.Clubs, CardRank.Seven, "\u6885\u82b17", "\u26637", 7),
                Card(CardSuit.Hearts, CardRank.Seven, "\u7ea2\u5fc37", "\u26657", 7),
                Card(CardSuit.Diamonds, CardRank.Seven, "\u65b9\u57577", "\u26667", 7),
                Card(CardSuit.Hearts, CardRank.Five, "\u7ea2\u5fc35", "\u26655", 5),
                Card(CardSuit.Diamonds, CardRank.Five, "\u65b9\u57575", "\u26665", 5),
                Card(CardSuit.Spades, CardRank.Eight, "\u9ed1\u68438", "\u26608", 8),
                Card(CardSuit.Clubs, CardRank.Eight, "\u6885\u82b18", "\u26638", 8)
            };
        }

        private static CardDefinition Card(CardSuit suit, CardRank rank, string displayName, string shortName, int pointValue)
        {
            return new CardDefinition(suit, rank, displayName, shortName, pointValue);
        }

        private static HandEvaluation NewEvaluation(HandCategory category, int primary, int secondary, int pointSum, string label)
        {
            return new HandEvaluation
            {
                Category = category,
                Primary = primary,
                Secondary = secondary,
                PointSum = pointSum,
                Label = label
            };
        }

        private static bool IsJokerThree(CardDefinition a, CardDefinition b)
        {
            return (a.IsJoker && b.Rank == CardRank.Three) || (b.IsJoker && a.Rank == CardRank.Three);
        }

        private static bool IsBomb(CardDefinition a, CardDefinition b)
        {
            return a.Rank == b.Rank && a.Color != CardColor.None && a.Color == b.Color;
        }

        private static bool TryGetSpecialCombo(CardDefinition a, CardDefinition b, out HandCategory category, out string label)
        {
            if (HasRanks(a, b, CardRank.Two, CardRank.Eight))
            {
                category = HandCategory.Special28;
                label = "28";
                return true;
            }

            if (HasRanks(a, b, CardRank.Queen, CardRank.Eight))
            {
                category = HandCategory.SpecialQ8;
                label = "Q8";
                return true;
            }

            if (HasRanks(a, b, CardRank.Queen, CardRank.Nine))
            {
                category = HandCategory.SpecialQ9;
                label = "Q9";
                return true;
            }

            category = HandCategory.PointSum;
            label = string.Empty;
            return false;
        }

        private static bool TryGetPowerCard(CardDefinition a, CardDefinition b, out CardDefinition powerCard)
        {
            bool aPower = a.IsPowerRank;
            bool bPower = b.IsPowerRank;

            if (aPower && !bPower)
            {
                powerCard = a;
                return true;
            }

            if (bPower && !aPower)
            {
                powerCard = b;
                return true;
            }

            if (aPower && bPower)
            {
                int aWeight = PowerRankWeight(a.Rank) * 10 + (a.Color == CardColor.Red ? 1 : 0);
                int bWeight = PowerRankWeight(b.Rank) * 10 + (b.Color == CardColor.Red ? 1 : 0);
                powerCard = aWeight >= bWeight ? a : b;
                return true;
            }

            powerCard = default(CardDefinition);
            return false;
        }

        private static bool HasRanks(CardDefinition a, CardDefinition b, CardRank first, CardRank second)
        {
            return (a.Rank == first && b.Rank == second) || (a.Rank == second && b.Rank == first);
        }

        private static int HighestRankWeight(CardDefinition a, CardDefinition b)
        {
            return Math.Max(RankWeight(a.Rank), RankWeight(b.Rank));
        }

        private static int RankWeight(CardRank rank)
        {
            switch (rank)
            {
                case CardRank.Joker:
                    return 100;
                case CardRank.Queen:
                    return 12;
                case CardRank.Jack:
                    return 11;
                case CardRank.Ten:
                    return 10;
                case CardRank.Nine:
                    return 9;
                case CardRank.Eight:
                    return 8;
                case CardRank.Seven:
                    return 7;
                case CardRank.Six:
                    return 6;
                case CardRank.Five:
                    return 5;
                case CardRank.Four:
                    return 4;
                case CardRank.Three:
                    return 3;
                case CardRank.Two:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int PowerRankWeight(CardRank rank)
        {
            switch (rank)
            {
                case CardRank.Queen:
                    return 4;
                case CardRank.Two:
                    return 3;
                case CardRank.Eight:
                    return 2;
                case CardRank.Four:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
