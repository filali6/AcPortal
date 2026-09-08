import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DrawerService {
    private _isOpen = new BehaviorSubject<boolean>(false);
    isOpen$ = this._isOpen.asObservable();

    open(): void { this._isOpen.next(true); }
    close(): void { this._isOpen.next(false); }
}