interface NamedCatalogItem {
  name: string;
}

interface PlatformCatalogItem
  extends NamedCatalogItem {
  source?: "Local" | "Igdb";
  id?: number;
}

const igdbPlatformPriorityIds = [
  6,   // PC (Microsoft Windows)
  167, // PlayStation 5
  48,  // PlayStation 4
  169, // Xbox Series X|S
  130, // Nintendo Switch
] as const;

const igdbPlatformPriorityMap = new Map<
  number,
  number
>(
  igdbPlatformPriorityIds.map(
    (id, index) => [id, index],
  ),
);

const companyPriorityGroups = [
  [
    "tencent",
    "tencent games",
  ],
  [
    "sony interactive entertainment",
    "sony computer entertainment",
    "playstation studios",
  ],
  [
    "xbox game studios",
    "microsoft game studios",
    "microsoft studios",
    "microsoft gaming",
  ],
  [
    "nintendo",
  ],
  [
    "take-two interactive",
    "take two interactive",
    "rockstar games",
    "rockstar north",
  ],
  [
    "electronic arts",
    "ea games",
    "ea sports",
  ],
  [
    "valve",
    "valve corporation",
  ],
  [
    "epic games",
  ],
  [
    "ubisoft",
  ],
  [
    "activision",
    "blizzard entertainment",
    "activision blizzard",
  ],
  [
    "netease",
    "netease games",
  ],
  [
    "capcom",
  ],
  [
    "bandai namco",
    "namco bandai",
  ],
  [
    "sega",
  ],
  [
    "square enix",
  ],
  [
    "riot games",
  ],
  [
    "konami",
  ],
  [
    "fromsoftware",
    "from software",
  ],
  [
    "cd projekt",
    "cd projekt red",
  ],
  [
    "warner bros games",
    "warner bros interactive",
    "wb games",
  ],
  [
    "bethesda",
    "zenimax",
  ],
  [
    "krafton",
  ],
  [
    "mihoyo",
    "hoyoverse",
  ],
  [
    "nexon",
  ],
  [
    "koei tecmo",
  ],
  [
    "devolver digital",
  ],
  [
    "larian studios",
  ],
  [
    "remedy entertainment",
  ],
  [
    "paradox interactive",
  ],
  [
    "focus entertainment",
  ],
];

const platformPriorityGroups = [
  [
    "pc microsoft windows",
    "microsoft windows",
    "windows pc",
    "windows",
  ],
  [
    "playstation 5",
    "ps5",
  ],
  [
    "xbox series x s",
    "xbox series x|s",
    "xbox series",
  ],
  [
    "nintendo switch 2",
    "switch 2",
  ],
  [
    "nintendo switch",
  ],
  [
    "steam deck",
  ],
  [
    "playstation 4",
    "ps4",
  ],
  [
    "xbox one",
  ],
  [
    "meta quest 3",
  ],
  [
    "playstation vr2",
    "ps vr2",
  ],
  [
    "android",
  ],
  [
    "ios",
    "iphone",
    "ipad",
  ],
  [
    "macos",
    "mac os",
  ],
  [
    "linux",
  ],
];

function normalizeCatalogName(
  value: string,
): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function getPriorityIndex(
  name: string,
  priorityGroups: string[][],
): number {
  const normalizedName =
    normalizeCatalogName(name);

  const index = priorityGroups.findIndex(
    (aliases) => {
      return aliases.some((alias) => {
        const normalizedAlias =
          normalizeCatalogName(alias);

        return normalizedName.includes(
          normalizedAlias,
        );
      });
    },
  );

  return index === -1
    ? Number.MAX_SAFE_INTEGER
    : index;
}

function sortByPriority<
  TItem extends NamedCatalogItem,
>(
  items: TItem[],
  priorityGroups: string[][],
): TItem[] {
  return [...items].sort(
    (firstItem, secondItem) => {
      const firstPriority =
        getPriorityIndex(
          firstItem.name,
          priorityGroups,
        );

      const secondPriority =
        getPriorityIndex(
          secondItem.name,
          priorityGroups,
        );

      if (
        firstPriority !== secondPriority
      ) {
        return (
          firstPriority -
          secondPriority
        );
      }

      return firstItem.name.localeCompare(
        secondItem.name,
        "az-AZ",
        {
          sensitivity: "base",
        },
      );
    },
  );
}

export function sortCompaniesByCurrentRelevance<
  TItem extends NamedCatalogItem,
>(items: TItem[]): TItem[] {
  return sortByPriority(
    items,
    companyPriorityGroups,
  );
}

export function sortPlatformsByCurrentRelevance<
  TItem extends PlatformCatalogItem,
>(items: TItem[]): TItem[] {
  return [...items].sort(
    (firstItem, secondItem) => {
      const firstIdPriority =
        firstItem.source === "Igdb" &&
        firstItem.id !== undefined
          ? igdbPlatformPriorityMap.get(
              firstItem.id,
            )
          : undefined;

      const secondIdPriority =
        secondItem.source === "Igdb" &&
        secondItem.id !== undefined
          ? igdbPlatformPriorityMap.get(
              secondItem.id,
            )
          : undefined;

      const firstPriority =
        firstIdPriority ??
        igdbPlatformPriorityIds.length +
          getPriorityIndex(
            firstItem.name,
            platformPriorityGroups,
          );

      const secondPriority =
        secondIdPriority ??
        igdbPlatformPriorityIds.length +
          getPriorityIndex(
            secondItem.name,
            platformPriorityGroups,
          );

      if (
        firstPriority !== secondPriority
      ) {
        return firstPriority - secondPriority;
      }

      return firstItem.name.localeCompare(
        secondItem.name,
        "az-AZ",
        {
          sensitivity: "base",
        },
      );
    },
  );
}

export function isPriorityCompany(
  name: string,
): boolean {
  return (
    getPriorityIndex(
      name,
      companyPriorityGroups,
    ) !== Number.MAX_SAFE_INTEGER
  );
}

export function isPriorityPlatform(
  name: string,
  source?: "Local" | "Igdb",
  id?: number,
): boolean {
  if (
    source === "Igdb" &&
    id !== undefined &&
    igdbPlatformPriorityMap.has(id)
  ) {
    return true;
  }

  return (
    getPriorityIndex(
      name,
      platformPriorityGroups,
    ) !== Number.MAX_SAFE_INTEGER
  );
}