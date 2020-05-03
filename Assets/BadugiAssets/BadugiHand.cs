using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

class BadugiHand
{
	private static readonly string[] __rankNames = new string[] {
		"(null)", "Ace", "Deuce", "Three", "Four", "Five", "Six",
		"Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King"
	};

	private bool analyzed = false;
	private List<int> internalRanking;
	private string internalContents;
	private string internalRankName;

	public readonly List<PlayingCard> hand;

	public string contentsString {
		get {
			if (!analyzed)
				throw new InvalidOperationException("Hand not analyzed");
			return internalContents;
		}
	}
	public string rankingString {
		get {
			if (!analyzed)
				throw new InvalidOperationException("Hand not analyzed");
			return internalRankName;
		}
	}

	private void AssignRanking()
	{
		List<PlayingCard> lowToHigh = hand.ToList();
		lowToHigh.Sort(PlayingCard.SortByLowRank);

		Dictionary<int, List<PlayingCard.Suit>> rankSuits = new Dictionary<int, List<PlayingCard.Suit>>();
		foreach (PlayingCard card in lowToHigh)
		{
			if (!rankSuits.ContainsKey(card.rank))
				rankSuits.Add(card.rank, new List<PlayingCard.Suit>());
			rankSuits[card.rank].Add(card.suit);
		}

		List<PlayingCard.Suit> usedSuits = new List<PlayingCard.Suit>();
		internalRanking = new List<int>();
		int previousPair = -1;
		foreach (KeyValuePair<int, List<PlayingCard.Suit>> entry in rankSuits)
		{
			int thisRank = entry.Key;
			if (entry.Value.Count() > 2)
			{
				// It is impossible for any rank with trips or quads to not be in the hand,
				// nor is it possible for them to have any effect on the remaining rank if any.
				internalRanking.Add(entry.Key);
			}
			else if (entry.Value.Count() == 1)
			{
				PlayingCard.Suit thisSuit = entry.Value[0];
				if (previousPair != -1 && rankSuits[previousPair].Contains(thisSuit))
				{
					// If there is an unresolved pair and this is in the same suit as one of
					// the cards in the pair, then add both this rank and the pair's rank
					// to the hand. The pair will use the suit we didn't use.
					internalRanking.Add(previousPair);
					internalRanking.Add(thisRank);
					usedSuits.AddRange(rankSuits[previousPair]);
					previousPair = -1;
				}
				else if (!usedSuits.Contains(thisSuit))
				{
					// Add a rank to the hand, if the suit isn't used already.
					internalRanking.Add(thisRank);
					usedSuits.Add(thisSuit);
				}
			}
			else
			{
				if (previousPair != -1)
				{
					// If there are two pairs, it's impossible for them to conflict.
					// Add them both to the hand.
					internalRanking.Add(previousPair);
					internalRanking.Add(thisRank);
					previousPair = -1;
				}
				else
				{
					// Is this pair resolved? Are one of the suits (or both) used up already?
					List<PlayingCard.Suit> notUsed = entry.Value.Except(usedSuits).ToList();
					if (notUsed.Count == 0)
					{
						// Both suits in this pair are already in use by better cards
						continue;
					}
					else if (notUsed.Count == 1)
					{
						// One suit is already in use by a better card, so we use the other one
						// ourselves, resolving the pair.
						internalRanking.Add(thisRank);
						usedSuits.Add(notUsed[0]);
					}
					else
					{
						// Both suits can be used -- this pair is unresolved.
						// Mark it and keep going on for now
						previousPair = thisRank;
					}
				}
			}
		}

		// If there's still an unresolved pair, then no other cards were in the same suits
		// as the card in the pair. Add that rank to the hand now.
		if (previousPair != -1)
			internalRanking.Add(previousPair);

		internalRanking.Sort();
		internalRanking.Reverse();
	}

	public BadugiHand(List<string> cards)
	{
		if (cards.Count != 4)
			throw new ArgumentException("cards has the wrong length; expected 4 elements");
		hand = cards.Select(s => new PlayingCard(s)).ToList();
	}

	public void AnalyzeHand()
	{
		if (analyzed)
			return;

		AssignRanking();

		internalContents = String.Format("{0} {1} {2} {3}", hand[0], hand[1], hand[2], hand[3]);

		string stringRank = String.Join(", ", internalRanking.Select(c => __rankNames[c]).ToArray());
		string worstCard = __rankNames[internalRanking[0]];

		if (internalRanking.Count == 4)
			internalRankName = String.Format("{1}-low Badugi ({0})", stringRank, worstCard);
		else if (internalRanking.Count == 3)
			internalRankName = String.Format("{1}-low three card hand ({0})", stringRank, worstCard);
		else if (internalRanking.Count == 2)
			internalRankName = String.Format("{1}-low two card hand ({0})", stringRank, worstCard);
		else
			internalRankName = String.Format("single {0}", worstCard);

		analyzed = true;
	}

	// Returns negative for the other hand winning, positive for this hand winning, zero for a tie
	public int CompareHands(BadugiHand otherHand)
	{
		if (!analyzed)
			throw new InvalidOperationException("Hand not analyzed");

		if (internalRanking.Count != otherHand.internalRanking.Count)
			return internalRanking.Count - otherHand.internalRanking.Count;
		for (int i = 0; i < internalRanking.Count; ++i)
		{
			if (internalRanking[i] != otherHand.internalRanking[i])
				return otherHand.internalRanking[i] - internalRanking[i];
		}
		return 0; // Tied.
	}

	public bool DistinctFrom(BadugiHand otherHand)
	{
		if (otherHand == null || otherHand.hand == null || hand == null)
			return true;
		foreach (PlayingCard card in otherHand.hand)
			if (hand.Where(c => c.rank == card.rank && c.suit == card.suit).Count() != 0)
				return false;
		return true;
	}

	public void SetFrontFaces(Transform[] cardObjects, Deck faces, Texture hiddenTexture)
	{
		for (int i = 0; i < cardObjects.Length; ++i)
		{
			MeshRenderer frontFace = cardObjects[i].Find("FrontFace").GetComponent<MeshRenderer>();
			Texture frontTex = (hiddenTexture != null && i >= 2) ? hiddenTexture : faces.GetFrontTexture(hand[i]);
			frontFace.material.mainTexture = frontTex;
			frontFace.sortingOrder = -4 + i;
		}
	}
}
