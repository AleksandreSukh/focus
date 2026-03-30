const COLOR_NAMES = [
  'black',
  'darkblue',
  'darkgreen',
  'darkcyan',
  'darkred',
  'darkmagenta',
  'darkyellow',
  'gray',
  'darkgray',
  'blue',
  'green',
  'cyan',
  'red',
  'magenta',
  'yellow',
  'white',
];

const COLOR_NAME_SET = new Set(COLOR_NAMES);
const COLOR_RESET_TAG = '!';
const COMMAND_START_BRACKET = '[';
const COMMAND_END_BRACKET = ']';
const URL_REGEX = /https?:\/\/[^\s<>"']+/gi;

export function parseInlineRuns(input) {
  const text = String(input ?? '');
  if (!text) {
    return [];
  }

  const runs = [];
  let pendingText = '';
  let activeColor = null;

  function flushPendingText() {
    if (!pendingText) {
      return;
    }

    if (runs.length > 0 && runs[runs.length - 1].colorName === activeColor) {
      runs[runs.length - 1] = {
        ...runs[runs.length - 1],
        text: `${runs[runs.length - 1].text}${pendingText}`,
      };
    } else {
      runs.push({
        text: pendingText,
        colorName: activeColor,
      });
    }

    pendingText = '';
  }

  for (let index = 0; index < text.length; index += 1) {
    const currentCharacter = text[index];
    if (currentCharacter !== COMMAND_START_BRACKET) {
      pendingText += currentCharacter;
      continue;
    }

    const commandEndIndex = text.indexOf(COMMAND_END_BRACKET, index + 1);
    if (commandEndIndex < 0) {
      pendingText += currentCharacter;
      continue;
    }

    const command = text.slice(index + 1, commandEndIndex);
    if (command === COLOR_RESET_TAG) {
      flushPendingText();
      activeColor = null;
      index = commandEndIndex;
      continue;
    }

    const normalizedCommand = command.toLowerCase();
    if (COLOR_NAME_SET.has(normalizedCommand)) {
      flushPendingText();
      activeColor = normalizedCommand;
      index = commandEndIndex;
      continue;
    }

    pendingText += text.slice(index, commandEndIndex + 1);
    index = commandEndIndex;
  }

  flushPendingText();
  return runs;
}

export function toPlainText(input) {
  return parseInlineRuns(input)
    .map((run) => run.text)
    .join('');
}

export function renderInlineHtml(input, options = {}) {
  const {
    theme = 'light',
    wrapperClass = 'formatted-inline',
  } = options;

  const content = parseInlineRuns(String(input ?? ''))
    .map((run) => renderRunHtml(run))
    .join('');

  const safeTheme = theme === 'dark' ? 'dark' : 'light';
  const classAttribute = wrapperClass
    ? ` class="${escapeHtml(wrapperClass)}"`
    : '';

  return `<span${classAttribute} data-inline-theme="${safeTheme}">${content}</span>`;
}

function renderRunHtml(run) {
  const content = renderTextWithLinks(run.text);
  if (!run.colorName) {
    return content;
  }

  return `<span class="color-${escapeHtml(run.colorName)}">${content}</span>`;
}

function renderTextWithLinks(text) {
  if (!text) {
    return '';
  }

  let currentIndex = 0;
  let html = '';

  for (const match of text.matchAll(URL_REGEX)) {
    const matchedText = match[0];
    const matchIndex = match.index ?? 0;
    if (matchIndex > currentIndex) {
      html += escapeHtml(text.slice(currentIndex, matchIndex));
    }

    const trimmedUrl = trimTrailingUrlPunctuation(matchedText);
    if (isSupportedHttpUrl(trimmedUrl)) {
      html += renderAnchor(trimmedUrl);
      if (trimmedUrl.length < matchedText.length) {
        html += escapeHtml(matchedText.slice(trimmedUrl.length));
      }
    } else {
      html += escapeHtml(matchedText);
    }

    currentIndex = matchIndex + matchedText.length;
  }

  if (currentIndex < text.length) {
    html += escapeHtml(text.slice(currentIndex));
  }

  return html;
}

function renderAnchor(url) {
  const encodedUrl = escapeHtml(url);
  return `<a href="${encodedUrl}" target="_blank" rel="noopener noreferrer">${encodedUrl}</a>`;
}

function isSupportedHttpUrl(candidate) {
  if (!candidate) {
    return false;
  }

  try {
    const url = new URL(candidate);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function trimTrailingUrlPunctuation(candidate) {
  let endIndex = candidate.length;
  while (endIndex > 0) {
    const trailingCharacter = candidate[endIndex - 1];
    if (
      shouldTrimSimpleTrailingPunctuation(trailingCharacter) ||
      isUnmatchedClosingDelimiter(candidate, endIndex, trailingCharacter)
    ) {
      endIndex -= 1;
      continue;
    }

    break;
  }

  return candidate.slice(0, endIndex);
}

function shouldTrimSimpleTrailingPunctuation(trailingCharacter) {
  return ['.', ',', ';', ':', '!', '?'].includes(trailingCharacter);
}

function isUnmatchedClosingDelimiter(candidate, endIndex, trailingCharacter) {
  switch (trailingCharacter) {
    case ')':
      return countOccurrences(candidate, '(', endIndex) < countOccurrences(candidate, ')', endIndex);
    case ']':
      return countOccurrences(candidate, '[', endIndex) < countOccurrences(candidate, ']', endIndex);
    case '}':
      return countOccurrences(candidate, '{', endIndex) < countOccurrences(candidate, '}', endIndex);
    default:
      return false;
  }
}

function countOccurrences(input, character, length) {
  let count = 0;
  for (let index = 0; index < length; index += 1) {
    if (input[index] === character) {
      count += 1;
    }
  }

  return count;
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
