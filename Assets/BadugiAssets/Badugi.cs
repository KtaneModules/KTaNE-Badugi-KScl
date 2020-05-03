using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using KModkit;

using RNG = UnityEngine.Random;

public class Badugi : MonoBehaviour
{
	// Standardized logging
	private static int globalLogID = 0;
	private int thisLogID;
	private bool moduleSolved;

	public KMBombInfo bombInfo;
	public KMAudio bombAudio;
	public KMBombModule bombModule;

	public KMRuleSeedable bombRuleSeed;
	private RuleSeedCardTable tableLayout;

	public KMSelectable selectLeft, selectRight;
	public Transform[] cardsLeft, cardsRight;
	private BadugiHand leftHand, rightHand;

	private int numCorrect = 0;

	public Animator[] chipAnims;

	// -----
	// Highlight/controls manip
	// -----

	private MeshRenderer[] internalHighlightRenderers;
	private bool readyForInput;

	void SetupHighlights()
	{
		internalHighlightRenderers = new MeshRenderer[2];
		try
		{
			internalHighlightRenderers[0] = selectLeft.transform.Find("Highlight(Clone)").GetComponent<MeshRenderer>();
			internalHighlightRenderers[1] = selectRight.transform.Find("Highlight(Clone)").GetComponent<MeshRenderer>();
		}
		catch
		{
			Debug.LogFormat("<Badugi #{0}> Couldn't get the internal highlight mesh objects.", thisLogID);
			return; // Whatever then.
		}

		foreach (MeshRenderer hl in internalHighlightRenderers)
		{
			hl.sortingOrder = -12;
			hl.enabled = false;
		}
	}

	void EnableInput()
	{
		readyForInput = true;
		if (internalHighlightRenderers[1] == null)
			return;

		foreach (MeshRenderer hl in internalHighlightRenderers)
			hl.enabled = true;		
	}

	void DisableInput()
	{
		readyForInput = false;
		if (internalHighlightRenderers[1] == null)
			return;

		foreach (MeshRenderer hl in internalHighlightRenderers)
			hl.enabled = false;		
	}

	// -----
	// Card visuals
	// -----

	public Animator[] cardAnims;

	public Deck[] frontList;
	private Deck frontInUse;
	public Texture hiddenFrontTexture;
	private bool cardsRevealed;

	public Texture[] backList;
	private Texture backInUse;
	public MeshRenderer deckTopCard;

	// Module settings
	public class BadugiSettings { public string deck; }
	public KMModSettings modSetup;

	void ModuleSettings()
	{
		BadugiSettings setup;
		string chosenDeck = "default";
		try
		{
			setup = JsonUtility.FromJson<BadugiSettings>(modSetup.Settings);
			chosenDeck = setup.deck;

			foreach (Deck d in frontList)
			{
				if (d.NameMatches(chosenDeck))
				{
					SetFront(d);
					return;
				}
			}
			Debug.LogFormat("<Badugi #{0}> No deck matches '{1}'! Using 'default' deck instead.", thisLogID, chosenDeck);
		}
		catch
		{
			Debug.LogFormat("<Badugi #{0}> Error reading module settings, using 'default' deck.", thisLogID);
		}
	}

	void SetRandomBacking()
	{
		MeshRenderer bf;

		backInUse = backList[RNG.Range(0,backList.Length)];
		for (int i = 0; i < cardsLeft.Length; ++i)
		{
			bf = cardsLeft[i].Find("BackFace").GetComponent<MeshRenderer>();
			bf.material.mainTexture = backInUse;
			bf.sortingOrder = -i - 5;
			bf = cardsRight[i].Find("BackFace").GetComponent<MeshRenderer>();
			bf.material.mainTexture = backInUse;
			bf.sortingOrder = -i - 5;
		}

		deckTopCard.material.mainTexture = backInUse;
		deckTopCard.sortingOrder = -10;
	}

	void SetFront(Deck newDeck)
	{
		frontInUse = newDeck;
		if (leftHand != null)
			leftHand.SetFrontFaces(cardsLeft, frontInUse, cardsRevealed ? null : hiddenFrontTexture);
		if (rightHand != null)
			rightHand.SetFrontFaces(cardsRight, frontInUse, cardsRevealed ? null : hiddenFrontTexture);
	}

	// -----
	// The Dirty Work™
	// -----

	BadugiHand GetRandomHand()
	{
		int rnX = RNG.Range(0, 10);
		int rnY = RNG.Range(0, 10);
		int rnD = RNG.Range(0, 8);
		return new BadugiHand(tableLayout.GetLine(rnX, rnY, (RuleSeedCardTable.TableReadDirection)rnD));
	}

	bool OnSelect(int side)
	{
		if (!readyForInput || moduleSolved)
			return false;

		chipAnims[numCorrect].Play((side > 0) ? "Bet Right" : "Bet Left");
		bombAudio.PlaySoundAtTransform("ChipPlace", gameObject.transform);
		StartCoroutine(RevealHands(side));
		return false;
	}

	IEnumerator RevealHands(int side)
	{
		DisableInput();
		cardsRevealed = true;
		leftHand.SetFrontFaces(cardsLeft, frontInUse, null);
		rightHand.SetFrontFaces(cardsRight, frontInUse, null);
		foreach (Animator a in cardAnims)
			a.Play("Showdown");
		yield return new WaitForSeconds(1.0f);

		int result = rightHand.CompareHands(leftHand);
		if (result <= 0)
			cardAnims[0].Play("Win");
		if (result >= 0)
			cardAnims[1].Play("Win");
		yield return new WaitForSeconds(0.4f);

		// If result is zero the hands are tied - no wrong answers are possible
		// If "right hand beats left hand" equals "player chose right hand" (both false or both true), player made the correct choice
		if ((result == 0) || ((result > 0) == (side > 0)))
		{
			bombAudio.PlaySoundAtTransform("ChipWin", gameObject.transform);
			chipAnims[numCorrect++].Play("Unhide");
			Debug.LogFormat("[Badugi #{0}] Selected the {1} hand; {2}/3 correct selections.", thisLogID, (side > 0) ? "right" : "left", numCorrect);
			if (numCorrect == 3)
			{
				Debug.LogFormat("[Badugi #{0}] SOLVE: Three correct selections made.", thisLogID);
				bombModule.HandlePass();
				moduleSolved = true;
				yield break;
			}
		}
		else
		{
			chipAnims[numCorrect].Play("Hide");
			Debug.LogFormat("[Badugi #{0}] STRIKE: Selected the {1} hand.", thisLogID, (side > 0) ? "right" : "left");
			bombModule.HandleStrike();
		}

		yield return new WaitForSeconds(2.0f);

		// Reshuffle cards animation.
		bombAudio.PlaySoundAtTransform("Shuffle", deckTopCard.transform);
		cardAnims[0].Play((result <= 0) ? "Shuffle From Up" : "Shuffle From Down");
		cardAnims[1].Play((result >= 0) ? "Shuffle From Up" : "Shuffle From Down");
		yield return new WaitForSeconds(1.25f);

		StartCoroutine(DealNewHand(false));
		yield break;
	}

	IEnumerator DealNewHand(bool first)
	{
		if (first) // Random initial wait.
			yield return new WaitForSeconds(RNG.Range(0.25f, 1.0f));

		leftHand = GetRandomHand();
		do
			rightHand = GetRandomHand();
		while (!rightHand.DistinctFrom(leftHand));

		leftHand.AnalyzeHand();
		rightHand.AnalyzeHand();
		Debug.LogFormat("[Badugi #{0}] The hand on the left is {1}—{3} {2}.", thisLogID, leftHand.contentsString,
			leftHand.rankingString, "aeiouAEIOU".Contains(leftHand.rankingString[0]) ? "an" : "a");
		Debug.LogFormat("[Badugi #{0}] The hand on the right is {1}—{3} {2}.", thisLogID, rightHand.contentsString,
			rightHand.rankingString, "aeiouAEIOU".Contains(rightHand.rankingString[0]) ? "an" : "a");

		int result = leftHand.CompareHands(rightHand);
		if (result < 0)
			Debug.LogFormat("[Badugi #{0}] The right hand is the better hand.", thisLogID);
		else if (result > 0)
			Debug.LogFormat("[Badugi #{0}] The left hand is the better hand.", thisLogID);
		else
			Debug.LogFormat("[Badugi #{0}] The two hands have equal strength. Either may be chosen.", thisLogID);

		cardsRevealed = false;
		leftHand.SetFrontFaces(cardsLeft, frontInUse, hiddenFrontTexture);
		rightHand.SetFrontFaces(cardsRight, frontInUse, hiddenFrontTexture);
		foreach (Animator a in cardAnims)
			a.Play("Draw");
		bombAudio.PlaySoundAtTransform("Deal", deckTopCard.transform);

		yield return new WaitForSeconds(1.0f);
		foreach (Animator a in cardAnims)
			a.Play("Reveal Two");
		yield return new WaitForSeconds(0.666667f);

		EnableInput();
		yield break;
	}

	void Awake()
	{
		thisLogID = ++globalLogID;

		// Rule seed support (mostly handled in another file)
		MonoRandom seededRNG = bombRuleSeed.GetRNG();
		Debug.LogFormat("[Badugi #{0}] Using rule seed: {1}", thisLogID, seededRNG.Seed);
		tableLayout = new RuleSeedCardTable(seededRNG);

		string testStr;
		int[] lens = new int[5];
		Dictionary<string, bool> seenBefore = new Dictionary<string, bool>();
		List<string> line;
		for (int y = 0; y < 10; ++y)
			for (int x = 0; x < 10; ++x)
				for (int i = 0; i < 8; ++i)
				{
					line = tableLayout.GetLine(x, y, (RuleSeedCardTable.TableReadDirection)i);
					testStr = String.Format("{0} {1}", line[0], line[1]);
					try {
						seenBefore.Add(testStr, true);						
					}
					catch (ArgumentException)
					{
						Debug.LogFormat("[Badugi #{0}] Card sequence {1} is duplicated.", thisLogID, testStr);
					}

					if (line[0] == line[3])
					{
						Debug.LogFormat("[Badugi #{0}] Card sequence {1} contains the same card multiple times.", thisLogID, testStr);
					}
					BadugiHand nh = new BadugiHand(line);
					nh.AnalyzeHand();
					++lens[nh.clength()];
				}
		if (seenBefore.Count == 800)
			Debug.LogFormat("[Badugi #{0}] No conflicts.", thisLogID);
		Debug.LogFormat("[Badugi #{0}] Badugis: {1}.", thisLogID, lens[4]);
		Debug.LogFormat("[Badugi #{0}] Threes: {1}.", thisLogID, lens[3]);
		Debug.LogFormat("[Badugi #{0}] Twos: {1}.", thisLogID, lens[2]);
		Debug.LogFormat("[Badugi #{0}] Singles: {1}.", thisLogID, lens[1]);

		// What cards would we like? Purely visual, no gameplay effects.
		SetRandomBacking();
		SetFront(frontList[0]); // Default, in case settings aren't valid/present
		Invoke("ModuleSettings", 0.5f); // Wait for things to load before checking settings

		selectLeft.OnInteract += delegate() { return OnSelect(-1); };
		selectRight.OnInteract += delegate() { return OnSelect(1); };

		readyForInput = false;
		Invoke("SetupHighlights", 0.25f);
	}

	void Start()
	{
		bombModule.OnActivate += delegate() { StartCoroutine(DealNewHand(true)); };
	}


	// -----
	// Twitch Plays support
	// -----

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Choose the left or right hand with '!{0} left' or '!{0} right'. Switch decks with '!{0} deck <name>'. (Try 'default' or 'four-color'; there may be others.)";
#pragma warning restore 414

	public IEnumerator ProcessTwitchCommand(string command)
	{
		Match mt;
		if ((mt = Regex.Match(command, @"^\s*(?:press|select|choose|pick)?\s*(l|left|r|right)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
		{
			if (!readyForInput)
			{
				yield return "sendtochaterror Cards are still being dealt.";
				yield break;
			}

			int side = (mt.Groups[1].ToString()[0] == 'l') ? -1 : 1;
			int result = rightHand.CompareHands(leftHand);

			yield return null;
			yield return new KMSelectable[] { (side < 0) ? selectLeft : selectRight };

			// If "right hand beats left hand" XOR "player chose right hand" (i.e., both aren't the same),
			// a strike is incoming for a wrong choice
			// If the result is 0 (tie) no strike is possible.
			if ((result != 0) && ((result > 0) ^ (side > 0)))
				yield return "strike";
			else if (numCorrect == 2) // Third correct answer is incoming
				yield return "solve";
		}
		else if ((mt = Regex.Match(command, @"^\s*(?:deck|cards)\s+([a-zA-Z0-9][a-zA-Z0-9\s\-]{0,20})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
		{
			string newDeck = mt.Groups[1].ToString();
			bool startsWithVowel = "aeiouAEIOU".Contains(newDeck[0]);
			foreach (Deck d in frontList)
			{
				if (d.NameMatches(newDeck))
				{
					// Don't yield return null. Don't focus the module for this, just do it immediately.
					// Changing out cards, a visual-only change, shouldn't penalize the player by wasting time.
					SetFront(d);
					yield return String.Format("sendtochat I've switched to {2} \"{0}\" deck at your request, {1}.", newDeck, "{0}", startsWithVowel ? "an" : "a");
					yield break;
				}
			}
			yield return String.Format("sendtochaterror I don't have {1} \"{0}\" deck, sorry.", newDeck, startsWithVowel ? "an" : "a");
			yield break;
		}
	}
/*
	void TwitchHandleForcedSolve()
	{
		if (moduleSolved)
			return;

		Debug.LogFormat("[Rainbow Arrows #{0}] SOLVE: Force solve requested by Twitch Plays.", thisLogID);
		moduleSolved = true;
		if (currentCoroutine != null)
			StopCoroutine(currentCoroutine);
		currentCoroutine = StartCoroutine(SolveAnimation());
	} */
}
