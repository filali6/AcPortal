import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface Tab {
  id: string;
  title: string;
  type: 'tasks' | 'create-project' | 'create-stream' | 'define-steps' | 'assign-manager';
  data?: any;
}

const TABS_KEY = 'acportal_tabs';
const ACTIVE_KEY = 'acportal_active_tab';

@Injectable({ providedIn: 'root' })
export class TabsService {
  private tabs$ = new BehaviorSubject<Tab[]>(this.loadTabs());
  private activeTabId$ = new BehaviorSubject<string>(this.loadActiveId());

  tabs = this.tabs$.asObservable();
  activeTabId = this.activeTabId$.asObservable();

  openTab(tab: Tab): void {
    const existing = this.tabs$.value.find(t => t.id === tab.id);
    if (!existing) {
      const updated = [...this.tabs$.value, tab];
      this.tabs$.next(updated);
      this.saveTabs(updated);        
    }
    this.activeTabId$.next(tab.id);
    this.saveActiveId(tab.id); 
    console.log(tab.id)      
  }

  closeTab(tabId: string): void {
    const tabs = this.tabs$.value.filter(t => t.id !== tabId);
    this.tabs$.next(tabs);
    this.saveTabs(tabs);             
    if (this.activeTabId$.value === tabId) {
      this.activeTabId$.next('tasks');
      this.saveActiveId('tasks');    
    }
  }

  setActiveTab(tabId: string): void {
    this.activeTabId$.next(tabId);
    this.saveActiveId(tabId);        
  }

  getCurrentActiveId(): string {
    return this.activeTabId$.value;
  }

 
  private loadTabs(): Tab[] {
    try {
      const raw = sessionStorage.getItem(TABS_KEY);
      
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }

  private loadActiveId(): string {
    try {
      return sessionStorage.getItem(ACTIVE_KEY) || 'tasks';
    } catch { return 'tasks'; }
  }

  private saveTabs(tabs: Tab[]): void {
    try { sessionStorage.setItem(TABS_KEY, JSON.stringify(tabs)); } catch {}
  }

  private saveActiveId(id: string): void {
    try { sessionStorage.setItem(ACTIVE_KEY, id); } catch {}
  }
}