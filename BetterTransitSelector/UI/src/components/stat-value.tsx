import * as l10n from "cs2/l10n";
import { useLocalization, type LocalizedNumberProps, type Unit } from "cs2/l10n";
import { useStatOptions } from "../vehicle-stats";

// `Unit` and `UnitSystem` are declared as enums in the .d.ts but are NOT in the runtime export
// map -- `UnitSystem.Freedom` threw "cannot read 'Freedom' of undefined" in game. The values
// themselves are stable strings and numbers, so they are spelled out here and typed back to the
// declared enum, which keeps the call sites type-checked without touching a runtime object that
// does not exist.
const UNIT_LENGTH = "length" as Unit;
const UNIT_INTEGER = "integer" as Unit;
const UNIT_FLOAT_1 = "floatSingleFraction" as Unit;
// 1 IS imperial: InterfaceSettings.UnitSystem is { Metric = 0, Freedom = 1 } in the game and the
// bundle alike, and the binding writes the raw int. Verified live against SharedSettings.
const UNIT_SYSTEM_FREEDOM = 1;

// The .d.ts declares `LocalizedNumber` twice: as an interface (a localization element type) and,
// via `LocalizedNumber$1 as LocalizedNumber`, as the component. In value position TypeScript picks
// the interface and refuses the JSX. The RUNTIME export is the component, so this reaches it past
// the type-level collision. Importing `LocalizedNumber$1` instead would type-check and then fail at
// render, because that name is not in the runtime export map -- the same trap useCachedLocalization
// set earlier.
const LocalizedNumber = (l10n as unknown as {
    LocalizedNumber: React.FC<LocalizedNumberProps>;
}).LocalizedNumber;

/**
 * A length, formatted by the game in the player's unit system -- metres or feet.
 */
export const LengthValue = ({ metres }: { metres: number }) => (
    <LocalizedNumber value={metres} unit={UNIT_LENGTH} />
);

/** A count, formatted by the game -- locale-aware digit grouping. */
export const CountValue = ({ value }: { value: number }) => (
    <LocalizedNumber value={value} unit={UNIT_INTEGER} />
);

const KPH_TO_MPH = 0.621371;

/**
 * A speed in the player's unit system.
 *
 * The game has no speed unit: vanilla never shows a vehicle's speed to the player, and the one
 * place it renders one -- the developer info panel -- is a hardcoded "km/h". So this reads the
 * unit system from the same settings the game's own formatters use and converts itself. The
 * number still goes through LocalizedNumber so the digit grouping matches everything else.
 */
export const SpeedValue = ({ kph }: { kph: number }) => {
    const { unitSettings } = useLocalization();
    const { speedUnit } = useStatOptions();
    // The mod's own setting wins; "Auto" defers to the game's unit system. The override exists
    // because the two do not always agree -- a UK player runs metric and still reads train
    // speeds in mph.
    const imperial =
        speedUnit === "Mph" ||
        (speedUnit !== "Kph" && unitSettings.unitSystem === UNIT_SYSTEM_FREEDOM);
    const value = imperial ? Math.round(kph * KPH_TO_MPH) : kph;

    return (
        <>
            <LocalizedNumber value={value} unit={UNIT_INTEGER} />
            {imperial ? " mph" : " km/h"}
        </>
    );
};

/**
 * Acceleration or braking, one decimal.
 *
 * Left in m/s² for both unit systems. Converting to ft/s² would be correct but unfamiliar even to
 * imperial-unit players, and no game surface displays acceleration at all to set a precedent.
 */
export const AccelerationValue = ({ mps2 }: { mps2: number }) => (
    <>
        <LocalizedNumber value={mps2} unit={UNIT_FLOAT_1} />
        {" m/s²"}
    </>
);
