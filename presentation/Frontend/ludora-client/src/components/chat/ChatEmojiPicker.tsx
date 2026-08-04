import { Search, SmilePlus, X } from "lucide-react";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
} from "react";
import { createPortal } from "react-dom";
import { useI18n } from "../../i18n/useI18n";

interface EmojiGroup {
  id: string;
  icon: string;
  labelAz: string;
  labelEn: string;
  emojis: string[];
}

const EMOJI_GROUPS: EmojiGroup[] = [
  {
    id: "smileys",
    icon: "😀",
    labelAz: "Üzlər",
    labelEn: "Smileys",
    emojis: [
      "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "🥲", "☺️",
      "😊", "😇", "🙂", "🙃", "😉", "😌", "😍", "🥰", "😘", "😗",
      "😙", "😚", "😋", "😛", "😝", "😜", "🤪", "🤨", "🧐", "🤓",
      "😎", "🥸", "🤩", "🥳", "😏", "😒", "😞", "😔", "😟", "😕",
      "🙁", "☹️", "😣", "😖", "😫", "😩", "🥺", "😢", "😭", "😤",
      "😠", "😡", "🤬", "🤯", "😳", "🥵", "🥶", "😱", "😨", "😰",
      "😥", "😓", "🤗", "🤔", "🫣", "🤭", "🫢", "🫡", "🤫", "🫠",
      "🤥", "😶", "🫥", "😐", "🫤", "😑", "😬", "🙄", "😯", "😦",
      "😧", "😮", "😲", "🥱", "😴", "🤤", "😪", "😵", "😵‍💫", "🤐",
      "🥴", "🤢", "🤮", "🤧", "😷", "🤒", "🤕", "🤑", "🤠", "😈",
      "👿", "👹", "👺", "🤡", "💩", "👻", "💀", "☠️", "👽", "👾",
      "🤖", "🎃", "😺", "😸", "😹", "😻", "😼", "😽", "🙀", "😿", "😾",
    ],
  },
  {
    id: "people",
    icon: "👋",
    labelAz: "İnsanlar",
    labelEn: "People",
    emojis: [
      "👋", "🤚", "🖐️", "✋", "🖖", "🫱", "🫲", "🫳", "🫴", "👌",
      "🤌", "🤏", "✌️", "🤞", "🫰", "🤟", "🤘", "🤙", "👈", "👉",
      "👆", "🖕", "👇", "☝️", "🫵", "👍", "👎", "✊", "👊", "🤛",
      "🤜", "👏", "🙌", "🫶", "👐", "🤲", "🤝", "🙏", "✍️", "💅",
      "🤳", "💪", "🦾", "🦿", "🦵", "🦶", "👂", "🦻", "👃", "🧠",
      "🫀", "🫁", "🦷", "🦴", "👀", "👁️", "👅", "👄", "🫦", "💋",
      "👶", "🧒", "👦", "👧", "🧑", "👱", "👨", "🧔", "👩", "🧓",
      "👴", "👵", "🙍", "🙎", "🙅", "🙆", "💁", "🙋", "🧏", "🙇",
      "🤦", "🤷", "👮", "🕵️", "💂", "🥷", "👷", "🫅", "🤴", "👸",
      "👳", "👲", "🧕", "🤵", "👰", "🤰", "🫃", "🫄", "🤱", "👼",
      "🎅", "🤶", "🦸", "🦹", "🧙", "🧚", "🧛", "🧜", "🧝", "🧞",
      "🧟", "💆", "💇", "🚶", "🧍", "🧎", "🏃", "💃", "🕺", "🕴️",
      "👯", "🧖", "🧗", "🤺", "🏇", "⛷️", "🏂", "🏌️", "🏄", "🚣",
      "🏊", "⛹️", "🏋️", "🚴", "🚵", "🤸", "🤼", "🤽", "🤾", "🤹",
    ],
  },
  {
    id: "nature",
    icon: "🐶",
    labelAz: "Heyvanlar",
    labelEn: "Animals",
    emojis: [
      "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐻‍❄️", "🐨",
      "🐯", "🦁", "🐮", "🐷", "🐽", "🐸", "🐵", "🙈", "🙉", "🙊",
      "🐒", "🐔", "🐧", "🐦", "🐤", "🐣", "🐥", "🦆", "🦅", "🦉",
      "🦇", "🐺", "🐗", "🐴", "🦄", "🐝", "🪱", "🐛", "🦋", "🐌",
      "🐞", "🐜", "🪰", "🪲", "🪳", "🦟", "🦗", "🕷️", "🕸️", "🦂",
      "🐢", "🐍", "🦎", "🦖", "🦕", "🐙", "🦑", "🪼", "🦐", "🦞",
      "🦀", "🐡", "🐠", "🐟", "🐬", "🐳", "🐋", "🦈", "🦭", "🐊",
      "🐅", "🐆", "🦓", "🫏", "🦍", "🦧", "🦣", "🐘", "🦛", "🦏",
      "🐪", "🐫", "🦒", "🦘", "🦬", "🐃", "🐂", "🐄", "🐎", "🐖",
      "🐏", "🐑", "🦙", "🐐", "🦌", "🫎", "🐕", "🐩", "🦮", "🐕‍🦺",
      "🐈", "🐈‍⬛", "🪽", "🪶", "🐓", "🦃", "🦤", "🦚", "🦜", "🦢",
      "🪿", "🦩", "🕊️", "🐇", "🦝", "🦨", "🦡", "🦫", "🦦", "🦥",
      "🐁", "🐀", "🐿️", "🦔", "🐾", "🐉", "🐲", "🌵", "🎄", "🌲",
      "🌳", "🌴", "🪹", "🪺", "🪵", "🌱", "🌿", "☘️", "🍀", "🎍",
      "🪴", "🎋", "🍃", "🍂", "🍁", "🍄", "🐚", "🪨", "🌾", "💐",
      "🌷", "🌹", "🥀", "🪻", "🪷", "🌺", "🌸", "🌼", "🌻", "🌞",
      "🌝", "🌚", "🌕", "🌙", "⭐", "🌟", "✨", "⚡", "☄️", "🔥",
      "🌈", "☀️", "🌤️", "⛅", "🌥️", "☁️", "🌦️", "🌧️", "⛈️", "🌩️",
      "🌨️", "❄️", "☃️", "⛄", "🌬️", "💨", "🌪️", "🌫️", "🌊", "💧",
    ],
  },
  {
    id: "food",
    icon: "🍕",
    labelAz: "Yemək",
    labelEn: "Food",
    emojis: [
      "🍏", "🍎", "🍐", "🍊", "🍋", "🍋‍🟩", "🍌", "🍉", "🍇", "🍓",
      "🫐", "🍈", "🍒", "🍑", "🥭", "🍍", "🥥", "🥝", "🍅", "🍆",
      "🥑", "🫛", "🥦", "🥬", "🥒", "🌶️", "🫑", "🌽", "🥕", "🫒",
      "🧄", "🧅", "🥔", "🍠", "🫚", "🥐", "🥯", "🍞", "🥖", "🥨",
      "🧀", "🥚", "🍳", "🧈", "🥞", "🧇", "🥓", "🥩", "🍗", "🍖",
      "🌭", "🍔", "🍟", "🍕", "🫓", "🥪", "🥙", "🧆", "🌮", "🌯",
      "🫔", "🥗", "🥘", "🫕", "🥫", "🍝", "🍜", "🍲", "🍛", "🍣",
      "🍱", "🥟", "🦪", "🍤", "🍙", "🍚", "🍘", "🍥", "🥠", "🥮",
      "🍢", "🍡", "🍧", "🍨", "🍦", "🥧", "🧁", "🍰", "🎂", "🍮",
      "🍭", "🍬", "🍫", "🍿", "🍩", "🍪", "🌰", "🥜", "🫘", "🍯",
      "🥛", "🍼", "☕", "🫖", "🍵", "🧃", "🥤", "🧋", "🍶", "🍺",
      "🍻", "🥂", "🍷", "🫗", "🍸", "🍹", "🍾", "🥃", "🧊", "🥄",
      "🍴", "🍽️", "🥣", "🥡", "🥢", "🧂",
    ],
  },
  {
    id: "activities",
    icon: "🎮",
    labelAz: "Fəaliyyət",
    labelEn: "Activities",
    emojis: [
      "⚽", "🏀", "🏈", "⚾", "🥎", "🎾", "🏐", "🏉", "🥏", "🎱",
      "🪀", "🏓", "🏸", "🏒", "🏑", "🥍", "🏏", "🪃", "🥅", "⛳",
      "🪁", "🏹", "🎣", "🤿", "🥊", "🥋", "🎽", "🛹", "🛼", "🛷",
      "⛸️", "🥌", "🎿", "⛷️", "🏂", "🪂", "🏋️", "🤼", "🤸", "⛹️",
      "🤺", "🤾", "🏌️", "🏇", "🧘", "🏄", "🏊", "🤽", "🚣", "🧗",
      "🚵", "🚴", "🏆", "🥇", "🥈", "🥉", "🏅", "🎖️", "🏵️", "🎗️",
      "🎫", "🎟️", "🎪", "🤹", "🎭", "🩰", "🎨", "🎬", "🎤", "🎧",
      "🎼", "🎹", "🥁", "🪘", "🎷", "🎺", "🪗", "🎸", "🪕", "🎻",
      "🪈", "🎲", "♟️", "🎯", "🎳", "🎮", "🎰", "🧩", "🎁", "🎉",
      "🎊", "🎈", "🎂", "🪅", "🪩", "🧨", "✨", "🎆", "🎇", "🧿",
    ],
  },
  {
    id: "travel",
    icon: "🚗",
    labelAz: "Səyahət",
    labelEn: "Travel",
    emojis: [
      "🚗", "🚕", "🚙", "🚌", "🚎", "🏎️", "🚓", "🚑", "🚒", "🚐",
      "🛻", "🚚", "🚛", "🚜", "🦯", "🦽", "🦼", "🛴", "🚲", "🛵",
      "🏍️", "🛺", "🚨", "🚔", "🚍", "🚘", "🚖", "🛞", "🚡", "🚠",
      "🚟", "🚃", "🚋", "🚞", "🚝", "🚄", "🚅", "🚈", "🚂", "🚆",
      "🚇", "🚊", "🚉", "✈️", "🛫", "🛬", "🛩️", "💺", "🛰️", "🚀",
      "🛸", "🚁", "🛶", "⛵", "🚤", "🛥️", "🛳️", "⛴️", "🚢", "⚓",
      "🛟", "🗺️", "🗿", "🗽", "🗼", "🏰", "🏯", "🏟️", "🎡", "🎢",
      "🎠", "⛲", "⛱️", "🏖️", "🏝️", "🏜️", "🌋", "⛰️", "🏔️", "🗻",
      "🏕️", "⛺", "🛖", "🏠", "🏡", "🏘️", "🏚️", "🏗️", "🏭", "🏢",
      "🏬", "🏣", "🏤", "🏥", "🏦", "🏨", "🏪", "🏫", "🏩", "💒",
      "🏛️", "⛪", "🕌", "🕍", "🛕", "🕋", "⛩️", "🛤️", "🛣️", "🗾",
      "🎑", "🏞️", "🌅", "🌄", "🌠", "🎇", "🎆", "🌇", "🌆", "🏙️",
      "🌃", "🌌", "🌉", "🌁",
    ],
  },
  {
    id: "objects",
    icon: "💡",
    labelAz: "Əşyalar",
    labelEn: "Objects",
    emojis: [
      "⌚", "📱", "📲", "💻", "⌨️", "🖥️", "🖨️", "🖱️", "🖲️", "🕹️",
      "🗜️", "💽", "💾", "💿", "📀", "📼", "📷", "📸", "📹", "🎥",
      "📽️", "🎞️", "📞", "☎️", "📟", "📠", "📺", "📻", "🎙️", "🎚️",
      "🎛️", "🧭", "⏱️", "⏲️", "⏰", "🕰️", "⌛", "⏳", "📡", "🔋",
      "🪫", "🔌", "💡", "🔦", "🕯️", "🪔", "🧯", "🛢️", "💸", "💵",
      "💴", "💶", "💷", "🪙", "💰", "💳", "💎", "⚖️", "🪜", "🧰",
      "🪛", "🔧", "🔨", "⚒️", "🛠️", "⛏️", "🪚", "🔩", "⚙️", "🪤",
      "🧱", "⛓️", "🧲", "🔫", "💣", "🧨", "🪓", "🔪", "🗡️", "⚔️",
      "🛡️", "🚬", "⚰️", "🪦", "⚱️", "🏺", "🔮", "📿", "🧿", "🪬",
      "💈", "⚗️", "🔭", "🔬", "🕳️", "🩹", "🩺", "🩻", "🩼", "💊",
      "💉", "🩸", "🧬", "🦠", "🧫", "🧪", "🌡️", "🧹", "🪠", "🧺",
      "🧻", "🚽", "🚿", "🛁", "🛀", "🧼", "🪥", "🪒", "🧽", "🪣",
      "🧴", "🔑", "🗝️", "🚪", "🪑", "🛋️", "🛏️", "🛌", "🧸", "🪆",
      "🖼️", "🪞", "🪟", "🛍️", "🛒", "🎁", "🎈", "🎏", "🎀", "🪄",
      "🪅", "🎊", "🎉", "🎎", "🏮", "🎐", "🧧", "✉️", "📩", "📨",
      "📧", "💌", "📥", "📤", "📦", "🏷️", "🪧", "📪", "📫", "📬",
      "📭", "📮", "📯", "📜", "📃", "📄", "📑", "🧾", "📊", "📈",
      "📉", "🗒️", "🗓️", "📆", "📅", "🗑️", "📇", "🗃️", "🗳️", "🗄️",
      "📋", "📁", "📂", "🗂️", "🗞️", "📰", "📓", "📔", "📒", "📕",
      "📗", "📘", "📙", "📚", "📖", "🔖", "🧷", "🔗", "📎", "🖇️",
      "📐", "📏", "🧮", "📌", "📍", "✂️", "🖊️", "🖋️", "✒️", "🖌️",
      "🖍️", "📝", "✏️", "🔍", "🔎", "🔏", "🔐", "🔒", "🔓",
    ],
  },
  {
    id: "symbols",
    icon: "❤️",
    labelAz: "Simvollar",
    labelEn: "Symbols",
    emojis: [
      "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "🩷",
      "🩵", "🩶", "💔", "❣️", "💕", "💞", "💓", "💗", "💖", "💘",
      "💝", "💟", "☮️", "✝️", "☪️", "🕉️", "☸️", "🪯", "✡️", "🔯",
      "🕎", "☯️", "☦️", "🛐", "⛎", "♈", "♉", "♊", "♋", "♌",
      "♍", "♎", "♏", "♐", "♑", "♒", "♓", "🆔", "⚛️", "🉑",
      "☢️", "☣️", "📴", "📳", "🈶", "🈚", "🈸", "🈺", "🈷️", "✴️",
      "🆚", "💮", "🉐", "㊙️", "㊗️", "🈴", "🈵", "🈹", "🈲", "🅰️",
      "🅱️", "🆎", "🆑", "🅾️", "🆘", "❌", "⭕", "🛑", "⛔", "📛",
      "🚫", "💯", "💢", "♨️", "🚷", "🚯", "🚳", "🚱", "🔞", "📵",
      "🚭", "❗", "❕", "❓", "❔", "‼️", "⁉️", "🔅", "🔆", "〽️",
      "⚠️", "🚸", "🔱", "⚜️", "🔰", "♻️", "✅", "🈯", "💹", "❇️",
      "✳️", "❎", "🌐", "💠", "Ⓜ️", "🌀", "💤", "🏧", "🚾", "♿",
      "🅿️", "🛗", "🈳", "🈂️", "🛂", "🛃", "🛄", "🛅", "🚹", "🚺",
      "🚼", "⚧️", "🚻", "🚮", "🎦", "📶", "🈁", "🔣", "ℹ️", "🔤",
      "🔡", "🔠", "🆖", "🆗", "🆙", "🆒", "🆕", "🆓", "0️⃣", "1️⃣",
      "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟", "🔢",
      "#️⃣", "*️⃣", "⏏️", "▶️", "⏸️", "⏯️", "⏹️", "⏺️", "⏭️", "⏮️",
      "⏩", "⏪", "⏫", "⏬", "◀️", "🔼", "🔽", "➡️", "⬅️", "⬆️",
      "⬇️", "↗️", "↘️", "↙️", "↖️", "↕️", "↔️", "↪️", "↩️", "⤴️",
      "⤵️", "🔀", "🔁", "🔂", "🔄", "🔃", "🎵", "🎶", "➕", "➖",
      "➗", "✖️", "🟰", "♾️", "💲", "💱", "™️", "©️", "®️", "〰️",
      "➰", "➿", "🔚", "🔙", "🔛", "🔝", "🔜", "✔️", "☑️", "🔘",
      "🔴", "🟠", "🟡", "🟢", "🔵", "🟣", "⚫", "⚪", "🟤", "🔺",
      "🔻", "🔸", "🔹", "🔶", "🔷", "🔳", "🔲", "▪️", "▫️", "◾",
      "◽", "◼️", "◻️", "🟥", "🟧", "🟨", "🟩", "🟦", "🟪", "⬛",
      "⬜", "🟫", "🔈", "🔇", "🔉", "🔊", "🔔", "🔕", "📣", "📢",
      "💬", "💭", "🗯️", "♠️", "♣️", "♥️", "♦️", "🃏", "🎴", "🀄",
      "🕐", "🕑", "🕒", "🕓", "🕔", "🕕", "🕖", "🕗", "🕘", "🕙", "🕚", "🕛",
    ],
  },
  {
    id: "flags",
    icon: "🏁",
    labelAz: "Bayraqlar",
    labelEn: "Flags",
    emojis: [
      "🏁", "🚩", "🎌", "🏴", "🏳️", "🏳️‍🌈", "🏳️‍⚧️", "🏴‍☠️", "🇦🇿", "🇹🇷",
      "🇬🇪", "🇷🇺", "🇺🇦", "🇺🇸", "🇨🇦", "🇲🇽", "🇧🇷", "🇦🇷", "🇨🇱", "🇨🇴",
      "🇬🇧", "🇮🇪", "🇫🇷", "🇩🇪", "🇮🇹", "🇪🇸", "🇵🇹", "🇳🇱", "🇧🇪", "🇨🇭",
      "🇦🇹", "🇵🇱", "🇨🇿", "🇸🇰", "🇭🇺", "🇷🇴", "🇧🇬", "🇬🇷", "🇸🇪", "🇳🇴",
      "🇩🇰", "🇫🇮", "🇮🇸", "🇪🇪", "🇱🇻", "🇱🇹", "🇷🇸", "🇭🇷", "🇸🇮", "🇧🇦",
      "🇦🇱", "🇲🇰", "🇲🇪", "🇲🇩", "🇧🇾", "🇦🇲", "🇰🇿", "🇺🇿", "🇹🇲", "🇰🇬",
      "🇹🇯", "🇨🇳", "🇯🇵", "🇰🇷", "🇮🇳", "🇵🇰", "🇧🇩", "🇱🇰", "🇳🇵", "🇧🇹",
      "🇹🇭", "🇻🇳", "🇲🇾", "🇸🇬", "🇮🇩", "🇵🇭", "🇲🇳", "🇦🇺", "🇳🇿", "🇿🇦",
      "🇪🇬", "🇲🇦", "🇩🇿", "🇹🇳", "🇳🇬", "🇰🇪", "🇪🇹", "🇬🇭", "🇸🇦", "🇦🇪",
      "🇶🇦", "🇰🇼", "🇮🇱", "🇯🇴", "🇱🇧", "🇮🇶", "🇮🇷", "🇦🇫", "🇵🇸", "🇺🇳",
    ],
  },
];

function pickerPosition(anchor: HTMLElement): CSSProperties {
  const rect = anchor.getBoundingClientRect();
  const width = Math.min(360, window.innerWidth - 24);
  const left = Math.min(
    Math.max(12, rect.left - 120),
    window.innerWidth - width - 12,
  );

  return {
    width,
    left,
    bottom: Math.max(12, window.innerHeight - rect.top + 10),
  };
}

export function ChatEmojiPicker({
  disabled = false,
  onSelect,
}: {
  disabled?: boolean;
  onSelect: (emoji: string) => void;
}) {
  const { language, t } = useI18n();
  const [open, setOpen] = useState(false);
  const [activeGroupId, setActiveGroupId] = useState(EMOJI_GROUPS[0].id);
  const [search, setSearch] = useState("");
  const [position, setPosition] = useState<CSSProperties>({});
  const triggerRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  const activeGroup =
    EMOJI_GROUPS.find((group) => group.id === activeGroupId) ?? EMOJI_GROUPS[0];
  const visibleEmojis = useMemo(() => {
    const normalized = search.trim().toLocaleLowerCase();
    if (!normalized) return activeGroup.emojis;

    const matchingGroups = EMOJI_GROUPS.filter((group) =>
      `${group.labelAz} ${group.labelEn} ${group.id}`
        .toLocaleLowerCase()
        .includes(normalized),
    );

    if (matchingGroups.length > 0) {
      return matchingGroups.flatMap((group) => group.emojis);
    }

    return EMOJI_GROUPS.flatMap((group) => group.emojis).filter((emoji) =>
      emoji.includes(search.trim()),
    );
  }, [activeGroup.emojis, search]);

  useEffect(() => {
    if (!open || !triggerRef.current) return;

    const updatePosition = () => {
      if (triggerRef.current) setPosition(pickerPosition(triggerRef.current));
    };
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (
        panelRef.current?.contains(target) ||
        triggerRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    updatePosition();
    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);
    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
        className="chat-composer-tool-button flex h-11 w-11 shrink-0 items-center justify-center rounded-xl transition disabled:cursor-not-allowed disabled:opacity-40"
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label={t(
          "messages:emoji.open",
          undefined,
          language === "en" ? "Choose emoji" : "Emoji seç",
        )}
      >
        <SmilePlus size={20} />
      </button>

      {open &&
        createPortal(
          <div
            ref={panelRef}
            role="dialog"
            aria-label={t(
              "messages:emoji.title",
              undefined,
              language === "en" ? "Emoji picker" : "Emoji seçimi",
            )}
            className="chat-emoji-picker fixed z-[190] overflow-hidden rounded-3xl"
            style={position}
          >
            <div className="chat-emoji-picker-header flex items-center gap-2 px-3 py-3">
              <label className="chat-emoji-search flex min-w-0 flex-1 items-center gap-2 rounded-xl px-3 py-2">
                <Search size={15} />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder={t(
                    "messages:emoji.search",
                    undefined,
                    language === "en" ? "Search emoji..." : "Emoji axtar...",
                  )}
                  className="min-w-0 flex-1 bg-transparent text-xs outline-none"
                />
              </label>
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="chat-emoji-close flex h-9 w-9 items-center justify-center rounded-xl transition"
                aria-label={t(
                  "messages:emoji.close",
                  undefined,
                  language === "en" ? "Close emoji picker" : "Emoji seçimini bağla",
                )}
              >
                <X size={17} />
              </button>
            </div>

            <div className="chat-emoji-categories flex gap-1 overflow-x-auto px-2 py-2">
              {EMOJI_GROUPS.map((group) => (
                <button
                  key={group.id}
                  type="button"
                  title={language === "en" ? group.labelEn : group.labelAz}
                  onClick={() => {
                    setActiveGroupId(group.id);
                    setSearch("");
                  }}
                  className={`chat-emoji-category flex h-9 min-w-9 items-center justify-center rounded-xl text-lg transition ${
                    activeGroup.id === group.id && !search ? "is-active" : ""
                  }`}
                >
                  {group.icon}
                </button>
              ))}
            </div>

            <div className="chat-emoji-grid max-h-72 overflow-y-auto p-3 pt-2">
              {visibleEmojis.map((emoji, index) => (
                <button
                  key={`${emoji}-${index}`}
                  type="button"
                  onClick={() => onSelect(emoji)}
                  className="chat-emoji-item flex h-10 w-10 items-center justify-center rounded-xl text-2xl transition"
                  aria-label={emoji}
                >
                  {emoji}
                </button>
              ))}
              {visibleEmojis.length === 0 && (
                <p className="chat-emoji-empty col-span-full px-3 py-10 text-center text-sm">
                  {t(
                    "messages:emoji.empty",
                    undefined,
                    language === "en" ? "No emoji found." : "Emoji tapılmadı.",
                  )}
                </p>
              )}
            </div>
          </div>,
          document.body,
        )}
    </>
  );
}
