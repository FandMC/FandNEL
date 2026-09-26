import {
  namePrefixes,
  nameStems,
  nameSuffixes,
  nameVowels,
  neteaseAdjectives,
  neteaseLocations,
  neteaseSubjects,
} from "./randomNicknameData";
import { readGatewaySettings } from "./settingsStorage";

function pick<T>(values: readonly T[]): T {
  return values[Math.floor(Math.random() * values.length)];
}

export function generateRandomNickname(
  neteaseFormat = readGatewaySettings().neteaseFormat,
): string {
  if (neteaseFormat) {
    let nickname: string;
    do {
      nickname = `${pick(neteaseAdjectives)}${pick(neteaseSubjects)}在${pick(neteaseLocations)}`;
    } while (nickname.length > 9);
    return nickname;
  }

  let nickname: string;
  do {
    const vowelCount = Math.random() < 0.7 ? 1 : 2;
    nickname = pick(namePrefixes);
    for (let index = 0; index < vowelCount; index += 1) nickname += pick(nameVowels);
    nickname += pick(nameStems);
    nickname += pick(nameSuffixes);
  } while (nickname.length < 5 || nickname.length > 9);
  return nickname;
}
