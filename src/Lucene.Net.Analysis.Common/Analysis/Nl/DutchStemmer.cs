﻿using System;
using System.Collections;
using System.Text;

namespace org.apache.lucene.analysis.nl
{

	/*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements.  See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License.  You may obtain a copy of the License at
	 *
	 *     http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */


	/// <summary>
	/// A stemmer for Dutch words. 
	/// <para>
	/// The algorithm is an implementation of
	/// the <a href="http://snowball.tartarus.org/algorithms/dutch/stemmer.html">dutch stemming</a>
	/// algorithm in Martin Porter's snowball project.
	/// </para> </summary>
	/// @deprecated (3.1) Use <seealso cref="org.tartarus.snowball.ext.DutchStemmer"/> instead, 
	/// which has the same functionality. This filter will be removed in Lucene 5.0 
	[Obsolete("(3.1) Use <seealso cref="org.tartarus.snowball.ext.DutchStemmer"/> instead,")]
	public class DutchStemmer
	{
	  private static readonly Locale locale = new Locale("nl", "NL");

	  /// <summary>
	  /// Buffer for the terms while stemming them.
	  /// </summary>
	  private StringBuilder sb = new StringBuilder();
	  private bool _removedE;
	  private IDictionary _stemDict;

	  private int _R1;
	  private int _R2;

	  //TODO convert to internal
	  /*
	   * Stems the given term to an unique <tt>discriminator</tt>.
	   *
	   * @param term The term that should be stemmed.
	   * @return Discriminator for <tt>term</tt>
	   */
	  public virtual string stem(string term)
	  {
		term = term.ToLower(locale);
		if (!isStemmable(term))
		{
		  return term;
		}
		if (_stemDict != null && _stemDict.Contains(term))
		{
		  if (_stemDict[term] is string)
		  {
			return (string) _stemDict[term];
		  }
		  else
		  {
			return null;
		  }
		}

		// Reset the StringBuilder.
		sb.Remove(0, sb.Length);
		sb.Insert(0, term);
		// Stemming starts here...
		substitute(sb);
		storeYandI(sb);
		_R1 = getRIndex(sb, 0);
		_R1 = Math.Max(3, _R1);
		step1(sb);
		step2(sb);
		_R2 = getRIndex(sb, _R1);
		step3a(sb);
		step3b(sb);
		step4(sb);
		reStoreYandI(sb);
		return sb.ToString();
	  }

	  private bool enEnding(StringBuilder sb)
	  {
		string[] enend = new string[]{"ene", "en"};
		for (int i = 0; i < enend.Length; i++)
		{
		  string end = enend[i];
		  string s = sb.ToString();
		  int index = s.Length - end.Length;
		  if (s.EndsWith(end, StringComparison.Ordinal) && index >= _R1 && isValidEnEnding(sb, index - 1))
		  {
			sb.Remove(index, index + end.Length - index);
			unDouble(sb, index);
			return true;
		  }
		}
		return false;
	  }


	  private void step1(StringBuilder sb)
	  {
		if (_R1 >= sb.Length)
		{
		  return;
		}

		string s = sb.ToString();
		int lengthR1 = sb.Length - _R1;
		int index;

		if (s.EndsWith("heden", StringComparison.Ordinal))
		{
		  sb.Remove(_R1, lengthR1 + _R1 - _R1).Insert(_R1, sb.Substring(_R1, lengthR1).replaceAll("heden", "heid"));
		  return;
		}

		if (enEnding(sb))
		{
		  return;
		}

		if (s.EndsWith("se", StringComparison.Ordinal) && (index = s.Length - 2) >= _R1 && isValidSEnding(sb, index - 1))
		{
		  sb.Remove(index, index + 2 - index);
		  return;
		}
		if (s.EndsWith("s", StringComparison.Ordinal) && (index = s.Length - 1) >= _R1 && isValidSEnding(sb, index - 1))
		{
		  sb.Remove(index, index + 1 - index);
		}
	  }

	  /// <summary>
	  /// Delete suffix e if in R1 and
	  /// preceded by a non-vowel, and then undouble the ending
	  /// </summary>
	  /// <param name="sb"> String being stemmed </param>
	  private void step2(StringBuilder sb)
	  {
		_removedE = false;
		if (_R1 >= sb.Length)
		{
		  return;
		}
		string s = sb.ToString();
		int index = s.Length - 1;
		if (index >= _R1 && s.EndsWith("e", StringComparison.Ordinal) && !isVowel(sb[index - 1]))
		{
		  sb.Remove(index, index + 1 - index);
		  unDouble(sb);
		  _removedE = true;
		}
	  }

	  /// <summary>
	  /// Delete "heid"
	  /// </summary>
	  /// <param name="sb"> String being stemmed </param>
	  private void step3a(StringBuilder sb)
	  {
		if (_R2 >= sb.Length)
		{
		  return;
		}
		string s = sb.ToString();
		int index = s.Length - 4;
		if (s.EndsWith("heid", StringComparison.Ordinal) && index >= _R2 && sb[index - 1] != 'c')
		{
		  sb.Remove(index, index + 4 - index); //remove heid
		  enEnding(sb);
		}
	  }

	  /// <summary>
	  /// <para>A d-suffix, or derivational suffix, enables a new word,
	  /// often with a different grammatical category, or with a different
	  /// sense, to be built from another word. Whether a d-suffix can be
	  /// attached is discovered not from the rules of grammar, but by
	  /// referring to a dictionary. So in English, ness can be added to
	  /// certain adjectives to form corresponding nouns (littleness,
	  /// kindness, foolishness ...) but not to all adjectives
	  /// (not for example, to big, cruel, wise ...) d-suffixes can be
	  /// used to change meaning, often in rather exotic ways.</para>
	  /// Remove "ing", "end", "ig", "lijk", "baar" and "bar"
	  /// </summary>
	  /// <param name="sb"> String being stemmed </param>
	  private void step3b(StringBuilder sb)
	  {
		if (_R2 >= sb.Length)
		{
		  return;
		}
		string s = sb.ToString();
		int index = 0;

		if ((s.EndsWith("end", StringComparison.Ordinal) || s.EndsWith("ing", StringComparison.Ordinal)) && (index = s.Length - 3) >= _R2)
		{
		  sb.Remove(index, index + 3 - index);
		  if (sb[index - 2] == 'i' && sb[index - 1] == 'g')
		  {
			if (sb[index - 3] != 'e' & index - 2 >= _R2)
			{
			  index -= 2;
			  sb.Remove(index, index + 2 - index);
			}
		  }
		  else
		  {
			unDouble(sb, index);
		  }
		  return;
		}
		if (s.EndsWith("ig", StringComparison.Ordinal) && (index = s.Length - 2) >= _R2)
		{
		  if (sb[index - 1] != 'e')
		  {
			sb.Remove(index, index + 2 - index);
		  }
		  return;
		}
		if (s.EndsWith("lijk", StringComparison.Ordinal) && (index = s.Length - 4) >= _R2)
		{
		  sb.Remove(index, index + 4 - index);
		  step2(sb);
		  return;
		}
		if (s.EndsWith("baar", StringComparison.Ordinal) && (index = s.Length - 4) >= _R2)
		{
		  sb.Remove(index, index + 4 - index);
		  return;
		}
		if (s.EndsWith("bar", StringComparison.Ordinal) && (index = s.Length - 3) >= _R2)
		{
		  if (_removedE)
		  {
			sb.Remove(index, index + 3 - index);
		  }
		  return;
		}
	  }

	  /// <summary>
	  /// undouble vowel
	  /// If the words ends CVD, where C is a non-vowel, D is a non-vowel other than I, and V is double a, e, o or u, remove one of the vowels from V (for example, maan -> man, brood -> brod).
	  /// </summary>
	  /// <param name="sb"> String being stemmed </param>
	  private void step4(StringBuilder sb)
	  {
		if (sb.Length < 4)
		{
		  return;
		}
		string end = StringHelperClass.SubstringSpecial(sb, sb.Length - 4, sb.Length);
		char c = end[0];
		char v1 = end[1];
		char v2 = end[2];
		char d = end[3];
		if (v1 == v2 && d != 'I' && v1 != 'i' && isVowel(v1) && !isVowel(d) && !isVowel(c))
		{
		  sb.Remove(sb.Length - 2, sb.Length - 1 - sb.Length - 2);
		}
	  }

	  /// <summary>
	  /// Checks if a term could be stemmed.
	  /// </summary>
	  /// <returns> true if, and only if, the given term consists in letters. </returns>
	  private bool isStemmable(string term)
	  {
		for (int c = 0; c < term.Length; c++)
		{
		  if (!char.IsLetter(term[c]))
		  {
			  return false;
		  }
		}
		return true;
	  }

	  /// <summary>
	  /// Substitute ä, ë, ï, ö, ü, á , é, í, ó, ú
	  /// </summary>
	  private void substitute(StringBuilder buffer)
	  {
		for (int i = 0; i < buffer.Length; i++)
		{
		  switch (buffer[i])
		  {
			case 'ä':
			case 'á':
			{
				buffer[i] = 'a';
				break;
			}
			case 'ë':
			case 'é':
			{
				buffer[i] = 'e';
				break;
			}
			case 'ü':
			case 'ú':
			{
				buffer[i] = 'u';
				break;
			}
			case 'ï':
			case 'i':
			{
				buffer[i] = 'i';
				break;
			}
			case 'ö':
			case 'ó':
			{
				buffer[i] = 'o';
				break;
			}
		  }
		}
	  }

	  /*private boolean isValidSEnding(StringBuilder sb) {
	    return isValidSEnding(sb, sb.length() - 1);
	  }*/

	  private bool isValidSEnding(StringBuilder sb, int index)
	  {
		char c = sb[index];
		if (isVowel(c) || c == 'j')
		{
		  return false;
		}
		return true;
	  }

	  /*private boolean isValidEnEnding(StringBuilder sb) {
	    return isValidEnEnding(sb, sb.length() - 1);
	  }*/

	  private bool isValidEnEnding(StringBuilder sb, int index)
	  {
		char c = sb[index];
		if (isVowel(c))
		{
		  return false;
		}
		if (c < 3)
		{
		  return false;
		}
		// ends with "gem"?
		if (c == 'm' && sb[index - 2] == 'g' && sb[index - 1] == 'e')
		{
		  return false;
		}
		return true;
	  }

	  private void unDouble(StringBuilder sb)
	  {
		unDouble(sb, sb.Length);
	  }

	  private void unDouble(StringBuilder sb, int endIndex)
	  {
		string s = sb.Substring(0, endIndex);
		if (s.EndsWith("kk", StringComparison.Ordinal) || s.EndsWith("tt", StringComparison.Ordinal) || s.EndsWith("dd", StringComparison.Ordinal) || s.EndsWith("nn", StringComparison.Ordinal) || s.EndsWith("mm", StringComparison.Ordinal) || s.EndsWith("ff", StringComparison.Ordinal))
		{
		  sb.Remove(endIndex - 1, endIndex - endIndex - 1);
		}
	  }

	  private int getRIndex(StringBuilder sb, int start)
	  {
		if (start == 0)
		{
		  start = 1;
		}
		int i = start;
		for (; i < sb.Length; i++)
		{
		  //first non-vowel preceded by a vowel
		  if (!isVowel(sb[i]) && isVowel(sb[i - 1]))
		  {
			return i + 1;
		  }
		}
		return i + 1;
	  }

	  private void storeYandI(StringBuilder sb)
	  {
		if (sb[0] == 'y')
		{
		  sb[0] = 'Y';
		}

		int last = sb.Length - 1;

		for (int i = 1; i < last; i++)
		{
		  switch (sb[i])
		  {
			case 'i':
			{
				if (isVowel(sb[i - 1]) && isVowel(sb[i + 1]))
				{
				  sb[i] = 'I';
				}
				break;
			}
			case 'y':
			{
				if (isVowel(sb[i - 1]))
				{
				  sb[i] = 'Y';
				}
				break;
			}
		  }
		}
		if (last > 0 && sb[last] == 'y' && isVowel(sb[last - 1]))
		{
		  sb[last] = 'Y';
		}
	  }

	  private void reStoreYandI(StringBuilder sb)
	  {
		string tmp = sb.ToString();
		sb.Remove(0, sb.Length);
		sb.Insert(0, tmp.replaceAll("I", "i").replaceAll("Y", "y"));
	  }

	  private bool isVowel(char c)
	  {
		switch (c)
		{
		  case 'e':
		  case 'a':
		  case 'o':
		  case 'i':
		  case 'u':
		  case 'y':
		  case 'è':
		  {
			  return true;
		  }
		}
		return false;
	  }

	  internal virtual IDictionary StemDictionary
	  {
		  set
		  {
			_stemDict = value;
		  }
	  }

	}

}