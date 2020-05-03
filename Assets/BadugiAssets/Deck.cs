using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
	public string[] Names;
	public Texture[] ClubsFaces;
	public Texture[] DiamondsFaces;
	public Texture[] HeartsFaces;
	public Texture[] SpadesFaces;

	public Texture GetFrontTexture(PlayingCard card)
	{
		switch (card.suit)
		{
			case PlayingCard.Suit.Clubs: return ClubsFaces[card.rank-1];
			case PlayingCard.Suit.Diamonds: return DiamondsFaces[card.rank-1];
			case PlayingCard.Suit.Hearts: return HeartsFaces[card.rank-1];
			case PlayingCard.Suit.Spades: return SpadesFaces[card.rank-1];
			default: return null;
		}
	}

	public bool NameMatches(string test)
	{
		foreach (string rx in Names)
		{
			if (Regex.IsMatch(test, rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				return true;
		}
		return false;
	}
}