import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabsService, Tab } from '../../services/tabs.service';

@Component({
  selector: 'app-tabs-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabs-bar.component.html',
  styleUrl: './tabs-bar.component.scss'
})
export class TabsBarComponent implements OnInit {
  tabs: Tab[] = [];
  activeTabId = 'tasks';

  constructor(private tabsService: TabsService) {}

  ngOnInit(): void {
    this.tabsService.tabs.subscribe(tabs => this.tabs = tabs);
    this.tabsService.activeTabId.subscribe(id => this.activeTabId = id);
  }

  setActive(tabId: string): void {
    this.tabsService.setActiveTab(tabId);
  }

  close(tabId: string, event: MouseEvent): void {
    event.stopPropagation();
    this.tabsService.closeTab(tabId);
  }
}