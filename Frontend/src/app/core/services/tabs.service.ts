import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface Tab {
  id: string;
  title: string;
  type: 'tasks' | 'create-project' | 'create-stream' | 'define-steps' | 'assign-manager';
  data?: any;
}

@Injectable({ providedIn: 'root' })
export class TabsService {
  private tabs$ = new BehaviorSubject<Tab[]>([]);
  private activeTabId$ = new BehaviorSubject<string>('tasks');

  tabs = this.tabs$.asObservable();
  activeTabId = this.activeTabId$.asObservable();

  openTab(tab: Tab): void {
    const existing = this.tabs$.value.find(t => t.id === tab.id);
    if (!existing) {
      this.tabs$.next([...this.tabs$.value, tab]);
    }
    this.activeTabId$.next(tab.id);
  }

  closeTab(tabId: string): void {
    const tabs = this.tabs$.value.filter(t => t.id !== tabId);
    this.tabs$.next(tabs);
    // Si on ferme l'onglet actif → revenir à tasks
    if (this.activeTabId$.value === tabId) {
      this.activeTabId$.next('tasks');
    }
  }

  setActiveTab(tabId: string): void {
    this.activeTabId$.next(tabId);
  }

  getCurrentActiveId(): string {
    return this.activeTabId$.value;
  }
}