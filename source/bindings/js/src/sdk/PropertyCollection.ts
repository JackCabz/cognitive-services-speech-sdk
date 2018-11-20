// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

import { PropertyId } from "./Exports";

/**
 * Represents collection of properties and their values.
 * @class PropertyCollection
 */
export class PropertyCollection {
    private keys: string[] = [] as string[];
    private values: string[] = [] as string[];

    /**
     * Returns the property value in type String. The parameter must have the same type as String.
     * Currently only String, int and bool are allowed.
     * If the name is not available, the specified defaultValue is returned.
     * @member PropertyCollection.prototype.getProperty
     * @function
     * @public
     * @param {string} key - The parameter name.
     * @param {string} def - The default value which is returned if the parameter is not available in the collection.
     * @returns {string} value of the parameter.
     */
    public getProperty(key: PropertyId | string, def?: string): string {
        let keyToUse: string;

        if (typeof key === "string") {
            keyToUse = key;
        } else {
            keyToUse = PropertyId[key];
        }

        for (let n = 0; n < this.keys.length; n++) {
            if (this.keys[n] === keyToUse) {
                return this.values[n];
            }
        }

        return def;
    }

    /**
     * Sets the String value of the parameter specified by name.
     * @member PropertyCollection.prototype.setProperty
     * @function
     * @public
     * @param {string} key - The parameter name.
     * @param {string} value - The value of the parameter.
     */
    public setProperty(key: string | PropertyId, value: string): void {
        let keyToUse: string;

        if (typeof key === "string") {
            keyToUse = key;
        } else {
            keyToUse = PropertyId[key];
        }

        for (let n = 0; n < this.keys.length; n++) {
            if (this.keys[n] === keyToUse) {
                this.values[n] = value;
                return;
            }
        }

        this.keys.push(keyToUse);
        this.values.push(value);
    }

    /**
     * Clones the collection.
     * @member PropertyCollection.prototype.clone
     * @function
     * @public
     * @returns {PropertyCollection} A copy of the collection.
     */
    public clone(): PropertyCollection {
        const clonedMap = new PropertyCollection();

        for (let n = 0; n < this.keys.length; n++) {
            clonedMap.keys.push(this.keys[n]);
            clonedMap.values.push(this.values[n]);
        }

        return clonedMap;
    }
}
