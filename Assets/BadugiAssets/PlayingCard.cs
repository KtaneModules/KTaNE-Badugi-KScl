using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayingCard
{
	public enum Suit {
		Spades = 0,
		Hearts,
		Diamonds,
		Clubs,
	};

	public readonly Suit suit;
	public readonly int  rank;

	public override string ToString()
	{
		int unicodeChr = 0x1F0A1;
		unicodeChr += 0x10 * (int)suit;
		unicodeChr += rank;
		if (rank <= 11)
			--unicodeChr;

		return Char.ConvertFromUtf32(unicodeChr).ToString();
	}

	public PlayingCard(string id)
	{
		if (id == null || id.Length != 2)
			throw new ArgumentException("Invalid initializer.");
		switch (id[0])
		{
			case 'A': rank = 1; break;
			case 'J': rank = 11; break;
			case 'Q': rank = 12; break;
			case 'K': rank = 13; break;
			case 'T': rank = 10; break;
			case 'C': throw new ArgumentException("This deck does not use Knights.");
			default: 
				rank = id[0] - '0';
				if (rank <= 1 || rank >= 10)
					throw new ArgumentException("Invalid rank supplied.");
				break;
		}
		switch (id[1])
		{
			case 'C': case '♣': case '♧': suit = Suit.Clubs; break;
			case 'D': case '♦': case '♢': suit = Suit.Diamonds; break;
			case 'H': case '♥': case '♡': suit = Suit.Hearts; break;
			case 'S': case '♠': case '♤': suit = Suit.Spades; break;
			default: throw new ArgumentException("Invalid suit supplied.");
		}
	}

	public static int SortByLowRank(PlayingCard x, PlayingCard y)
	{
		return x.rank - y.rank;
	}
}
