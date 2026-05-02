import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
  duration?: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private toasts$ = new BehaviorSubject<Toast[]>([]);
  toasts = this.toasts$.asObservable();

  show(message: string, type: 'success' | 'error' | 'info' = 'success', duration = 3000): void {
    const id = Date.now().toString();
    const toast: Toast = { id, message, type, duration };
    this.toasts$.next([...this.toasts$.value, toast]);
    setTimeout(() => this.remove(id), duration);
  }

  remove(id: string): void {
    this.toasts$.next(this.toasts$.value.filter(t => t.id !== id));
  }
}