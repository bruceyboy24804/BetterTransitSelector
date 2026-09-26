import * as l10n from "cs2/l10n";
import { useLocalization, type LocalizedNumberProps, type Unit } from "cs2/l10n";
import { useStatOptions } from "../vehicle-stats";

const UNIT_LENGTH = "length" as Unit;
const UNIT_INTEGER = "integer" as Unit;
const UNIT_FLOAT_1 = "floatSingleFraction" as Unit;

const UNIT_SYSTEM_FREEDOM = 1;

const LocalizedNumber = (l10n as unknown as {
    LocalizedNumber: React.FC<LocalizedNumberProps>;
}).LocalizedNumber;

export const LengthValue = ({ metres }: { metres: number }) => (
    <LocalizedNumber value={metres} unit={UNIT_LENGTH} />
);

export const CountValue = ({ value }: { value: number }) => (
    <LocalizedNumber value={value} unit={UNIT_INTEGER} />
);

const KPH_TO_MPH = 0.621371;

export const SpeedValue = ({ kph }: { kph: number }) => {
    const { unitSettings } = useLocalization();
    const { speedUnit } = useStatOptions();

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

export const AccelerationValue = ({ mps2 }: { mps2: number }) => (
    <>
        <LocalizedNumber value={mps2} unit={UNIT_FLOAT_1} />
        {" m/s²"}
    </>
);
