# TTS Sanitization Component Outline

This document provides a technical overview of `TTSSystem.Sanitize.cs`, which handles the transformation, cleaning, and normalization of text before it is sent to the Text-to-Speech (TTS) engine.

## Namespace: `Content.Server.TTS`

### Class: `TTSSystem` (Partial)

The `TTSSystem` handles the logic for preparing raw chat messages for synthesis.

#### Methods

- **`OnTransformSpeech(TransformSpeechEvent args)`**
    - **Purpose**: A pre-sanitization hook that removes specific syntax markers.
    - **Behavior**: Currently removes the `+` character from messages. This is typically used to strip emphasis or control characters that shouldn't be spoken.

- **`Sanitize(string text)` (Internal Static)**
    - **Purpose**: The primary pipeline for cleaning text.
    - **Pipeline Steps**:
        1. **Trim**: Removes leading/trailing whitespace.
        2. **Character Filtering**: Strips all characters EXCEPT:
            - English letters (`a-z`, `A-Z`)
            - Russian/Ukrainian letters (`а-я`, `А-Я`, `ё`, `Ё`, `Є-Я`, `Ґ`, `а-ї`, `ґ`)
            - Digits (`0-9`)
            - Punctuation/Markers: `-`, `,`, `\+`, `?`, `!`, `.`, `'`, `’`, and spaces.
        3. **Word Replacement**: Uses the `WordReplacement` dictionary to perform phonetic or literal substitutions on whole words.
        4. **Decimal Normalization**: Replaces periods/commas between digits with the Ukrainian term `" цілих "` (e.g., "1.5" becomes "1 цілих 5").
        5. **Number Normalization**: Converts sequences of digits into spoken words using `NumberConverter`.
        6. **Punctuation Squashing**:
            - Squashes sequences of 2 or more `!` into a single `!`.
            - Squashes sequences of 2 or more `?` into a single `?`.
            - Squashes sequences of 2 or more `'` or `’` (apostrophes) into a single `'`.
            - Squashes sequences of 4 or more `.` into a single ellipsis `...`.
            - *Note*: Sequences of 2 or 3 dots are preserved for brief pauses.
        7. **Global De-Spamming**:
            - Identifies any character (excluding digits and dots) that repeats 3 or more times.
            - Squashes the sequence down to exactly **two** instances.
            - This prevents "text screaming" (e.g., `Heeeeeelllooooo` $\rightarrow$ `Heelloo`) while preserving legitimate double letters.
        8. **Final Trim**: Ensures clean boundaries.

- **`ReplaceMatchedWord(Match word)`**
    - **Purpose**: Callback for the word replacement regex. Performs a case-insensitive lookup in `WordReplacement`.

- **`ReplaceWord2Num(Match word)`**
    - **Purpose**: Callback for the digit replacement regex. Parses the match as a `long` and passes it to `NumberConverter.NumberToText`.

#### Data Structures

- **`WordReplacement`**
    - **Type**: `IReadOnlyDictionary<string, string>`
    - **Contents**: A mapping of lowercase source words to their spoken equivalents.
    - **Example**: `{ "sss", "s" }` (used to normalize sibilants or specific character speech quirks).

---

### Class: `NumberConverter` (Static)

A utility class designed to convert numerical values into word representations.

#### Features

- **Language Logic**: While the word arrays (`Frac20Male`, etc.) are in **English**, the structure (specifically `GetDeclension`) is built to handle **Slavic grammar rules** (Ukrainian/Russian declensions for 1, 2-4, and 5+ counts).
- **Scale**: Supports numbers up to **999 Trillion**.
- **Negative Numbers**: Prepends the word "negative" for values below zero.

#### Data Arrays

- `Frac20Male` / `Frac20Female`: Words for 1-19.
- `Hunds`: Words for hundreds (100-900).
- `Tens`: Words for tens (10-90).

#### Core Methods

- **`NumberToText(long value, bool male = true)`**
    - Recursively processes the number by "periods" (trillion, billion, million, thousand) and then handles the remaining hundreds/tens/units.
- **`AppendPeriod(...)`**
    - Helper that extracts a power-of-1000 chunk, converts it to text, and appends the appropriate scale word (e.g., "million").
- **`GetDeclension(int val, string one, string two, string five)`**
    - Implements the [Slavic pluralization rule](https://unicode-org.github.io/cldr-staging/charts/37/supplemental/language_plural_rules.html#uk):
        - Ends in 1 (but not 11) -> `one`
        - Ends in 2, 3, 4 (but not 12, 13, 14) -> `two`
        - Other (ends in 0, 5-9, or 11-14) -> `five`
