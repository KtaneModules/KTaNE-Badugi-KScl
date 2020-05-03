using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

class RuleSeedCardTable {
	private static readonly int[][] __adjacentChecks = new int[][] {
		new int[] {0, 9},
		new int[] {1, 9},
		new int[] {1, 0},
		new int[] {1, 1},
		new int[] {0, 1},
		new int[] {9, 1},
		new int[] {9, 0},
		new int[] {9, 9},
	};
	private static readonly int[] __dupeDetect = new int[] { 0, 3, 7 };

	// This is Rule Seed -228.
	// Negative rule seeds are actually inaccessible in the game proper,
	// so it makes for a good "default" that shouldn't be identical
	// to any normally-usable rule seed.
	private static readonly string[,] fallbackCardTable = new string[,] {
		{"8♦", "3♦", "5♦", "4♠", "9♠", "7♠", "5♣", "7♦", "2♥", "2♠"},
		{"5♠", "9♣", "8♣", "6♥", "6♦", "2♣", "J♣", "J♠", "Q♣", "5♣"},
		{"6♣", "7♣", "2♦", "J♥", "A♠", "2♥", "6♥", "3♣", "A♦", "K♥"},
		{"9♠", "Q♦", "A♣", "7♥", "6♠", "3♠", "9♦", "5♠", "5♦", "8♥"},
		{"6♠", "9♥", "9♦", "6♦", "3♦", "A♥", "J♥", "2♣", "4♦", "4♥"},
		{"7♣", "J♠", "T♠", "8♠", "2♦", "4♣", "Q♠", "K♠", "K♣", "4♣"},
		{"2♠", "5♥", "3♥", "T♦", "9♥", "J♦", "6♣", "8♠", "9♣", "A♠"},
		{"T♥", "J♣", "4♠", "Q♣", "5♥", "Q♥", "3♠", "K♦", "3♣", "Q♠"},
		{"8♥", "A♣", "A♥", "4♦", "T♣", "A♦", "8♣", "8♦", "7♥", "3♥"},
		{"T♣", "T♦", "7♦", "T♥", "J♦", "T♠", "4♥", "Q♦", "Q♥", "7♠"}
	};

	private string[,] cardTable;

	private bool PlaceCardsUntilEmpty(MonoRandom seededRNG, List<string> cardsToPlace, List<int> locationsOpen)
	{
		List<int> restrictedOpen;
		int mainLoc, x, y, lx, ly, auxLoc, aux_x, aux_y;
		List<string> adjacentCards;

		foreach (string card in cardsToPlace)
		{
			// The first card can go anywhere. Literally anywhere. We don't care.
			mainLoc = locationsOpen[seededRNG.Next(locationsOpen.Count)];
			x = mainLoc % 10;
			y = mainLoc / 10;
			cardTable[x, y] = card;
			locationsOpen.Remove(mainLoc);
			//Debug.LogFormat("{0} -> {1}, {2}", card, x, y);

			// Check all spaces adjacent to the first card. If they're occupied,
			// keep track of them for later.
			adjacentCards = new List<string>() { card };
			foreach (int[] adj in __adjacentChecks)
			{
				lx = (x + adj[0]) % 10;
				ly = (y + adj[1]) % 10;
				if (!String.IsNullOrEmpty(cardTable[lx, ly]))
					adjacentCards.Add(cardTable[lx, ly]);
			}

			// Keep track of all locations that were open, but we've rejected...
			restrictedOpen = locationsOpen.ToList();

			retryAuxCard:
			//Debug.LogFormat("attempt: remaining {0}", restrictedOpen.Count);
			if (restrictedOpen.Count == 0)
				return false; // failure

			auxLoc = restrictedOpen[seededRNG.Next(restrictedOpen.Count)];
			restrictedOpen.Remove(auxLoc);
			aux_x = auxLoc % 10;
			aux_y = auxLoc / 10;
			//Debug.LogFormat("{0} -> {1}, {2}?", card, aux_x, aux_y);

			// If we're trying to place the second card extremely close to the first, reject immediately.
			if ((Math.Abs(aux_x - x) <= 2 || Math.Abs(aux_x - x) >= 8) && (Math.Abs(aux_y - y) <= 2 || Math.Abs(aux_y - y) >= 8))
				goto retryAuxCard;

			// Prevent locations that would result in a hand having two of the same card.
			if (__dupeDetect.Contains(Math.Abs(aux_x - x)) && __dupeDetect.Contains(Math.Abs(aux_y - y)))
				goto retryAuxCard;

			// If we're next to a card that the first card is also next to, reject. (Causes ambiguity.)
			foreach (int[] adj in __adjacentChecks)
			{
				lx = (aux_x + adj[0]) % 10;
				ly = (aux_y + adj[1]) % 10;
				if (adjacentCards.Contains(cardTable[lx, ly]))
					goto retryAuxCard;
			}

			// Location is good, move on
			cardTable[aux_x, aux_y] = card;
			locationsOpen.Remove(auxLoc);
			//Debug.LogFormat("{0} -> {1}, {2}", card, aux_x, aux_y);
		}
		return true; // success
	}

	public RuleSeedCardTable(MonoRandom seededRNG)
	{
		// "Why not just do a basic shuffle and call it a day?"
		// That might result in ambiguous lines in the table.
		// This whole mess is to avoid that very situation, so all 800 possible
		// lines that the module can generate show unique pairs of cards.

		cardTable = new string[10, 10];
		List<int> locationsOpen = Enumerable.Range(0, 100).ToList();

		// We'll start by placing A-8 mostly randomly.
		List<string> cardsToPlace = new List<string>() {
			"8♣", "7♣", "6♣", "5♣", "4♣", "3♣", "2♣", "A♣", 
			"8♠", "7♠", "6♠", "5♠", "4♠", "3♠", "2♠", "A♠", 
			"8♥", "7♥", "6♥", "5♥", "4♥", "3♥", "2♥", "A♥",
			"8♦", "7♦", "6♦", "5♦", "4♦", "3♦", "2♦", "A♦"
		};
		PlaceCardsUntilEmpty(seededRNG, cardsToPlace, locationsOpen);

		// And then, attempt to place Q-9 in positions that make every pair of cards
		// unique. If that fails, we retry from this point. If that fails ten times,
		// we throw up our hands, give up, and revert to a known-good setup.
		// It's better than just breaking for no discernable reason.
		List<int> backupLocationsOpen = locationsOpen.ToList();
		string[,] backupCardTable = (string[,])cardTable.Clone();

		int retryCount = 0;
		for (; retryCount < 10; ++retryCount)
		{
			cardsToPlace = new List<string>() {
				"Q♣", "Q♠", "Q♥", "Q♦",
				"J♣", "J♠", "J♥", "J♦",
				"T♣", "T♠", "T♥", "T♦",
				"9♣", "9♠", "9♥", "9♦"
			};
			seededRNG.ShuffleFisherYates(cardsToPlace);
			if (PlaceCardsUntilEmpty(seededRNG, cardsToPlace, locationsOpen))
				break;

			locationsOpen = backupLocationsOpen.ToList();
			cardTable = (string[,])backupCardTable.Clone();
		}

		// Did we seriously fail? Oh well. Backup table it is.
		if (retryCount >= 10)
		{
			cardTable = (string[,])fallbackCardTable.Clone();
			return;
		}

		// Kings only show up once. We don't need to worry about duplicates.
		// So, we just fill in the last four slots with them.
		cardsToPlace = new List<string>() { "K♣", "K♠", "K♥", "K♦" };
		seededRNG.ShuffleFisherYates(cardsToPlace);
		for (int i = 0; i < 4; ++i)
			cardTable[locationsOpen[i] % 10, locationsOpen[i] / 10] = cardsToPlace[i];

/*		string a = "";
		for (int y = 0; y < 10; ++y)
		{
			for (int x = 0; x < 10; ++x)
			{
				a += (cardTable[x, y] + " ");
			}
			Debug.LogFormat("{0}", a);
			a = "";
		} */
	}

	public enum TableReadDirection {
		Up = 0,
		UpRight,
		Right,
		DownRight,
		Down,
		DownLeft,
		Left,
		UpLeft,
	}

	public List<string> GetLine(int startX, int startY, TableReadDirection dir)
	{
		List<string> ret = new List<string>() { cardTable[startX, startY] };
		switch (dir)
		{
			case TableReadDirection.Up:
				ret.Add( cardTable[ startX,           (startY + 9) % 10] );
				ret.Add( cardTable[ startX,           (startY + 8) % 10] );
				ret.Add( cardTable[ startX,           (startY + 7) % 10] );
				break;
			case TableReadDirection.UpRight:
				ret.Add( cardTable[(startX + 1) % 10, (startY + 9) % 10] );
				ret.Add( cardTable[(startX + 2) % 10, (startY + 8) % 10] );
				ret.Add( cardTable[(startX + 3) % 10, (startY + 7) % 10] );
				break;
			case TableReadDirection.Right:
				ret.Add( cardTable[(startX + 1) % 10,  startY          ] );
				ret.Add( cardTable[(startX + 2) % 10,  startY          ] );
				ret.Add( cardTable[(startX + 3) % 10,  startY          ] );
				break;
			case TableReadDirection.DownRight:
				ret.Add( cardTable[(startX + 1) % 10, (startY + 1) % 10] );
				ret.Add( cardTable[(startX + 2) % 10, (startY + 2) % 10] );
				ret.Add( cardTable[(startX + 3) % 10, (startY + 3) % 10] );
				break;
			case TableReadDirection.Down:
				ret.Add( cardTable[ startX,           (startY + 1) % 10] );
				ret.Add( cardTable[ startX,           (startY + 2) % 10] );
				ret.Add( cardTable[ startX,           (startY + 3) % 10] );
				break;
			case TableReadDirection.DownLeft:
				ret.Add( cardTable[(startX + 9) % 10, (startY + 1) % 10] );
				ret.Add( cardTable[(startX + 8) % 10, (startY + 2) % 10] );
				ret.Add( cardTable[(startX + 7) % 10, (startY + 3) % 10] );
				break;
			case TableReadDirection.Left:
				ret.Add( cardTable[(startX + 9) % 10,  startY          ] );
				ret.Add( cardTable[(startX + 8) % 10,  startY          ] );
				ret.Add( cardTable[(startX + 7) % 10,  startY          ] );
				break;
			case TableReadDirection.UpLeft:
				ret.Add( cardTable[(startX + 9) % 10, (startY + 9) % 10] );
				ret.Add( cardTable[(startX + 8) % 10, (startY + 8) % 10] );
				ret.Add( cardTable[(startX + 7) % 10, (startY + 7) % 10] );
				break;
		}
		return ret;
	}
}