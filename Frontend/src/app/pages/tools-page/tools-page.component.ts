import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PluginBridgeService, Plugin } from '../../core/services/plugin-bridge.service';
import { TranslateModule } from '@ngx-translate/core';
import { DrawerService } from '../../core/services/drawer.service';

@Component({
  selector: 'app-tools-page',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './tools-page.component.html',
  styleUrl: './tools-page.component.scss'
})
export class ToolsPageComponent implements OnInit {
  myPlugins: Plugin[] = [];
  allPlugins: Plugin[] = [];
  loading = true;
  marketplaceOpen = false;

  constructor(
    private pluginBridge: PluginBridgeService,
    private router: Router,
    private drawerService: DrawerService
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.pluginBridge.getAllPlugins().subscribe({
      next: (all) => {
        this.allPlugins = all;
        this.pluginBridge.getMyPlugins().subscribe({
          next: (my) => {
            this.myPlugins = my;
            this.loading = false;
          }
        });
      }
    });
  }

  isAdded(pluginId: string): boolean {
    return this.myPlugins.some(p => p.id === pluginId);
  }

  addPlugin(pluginId: string): void {
    this.pluginBridge.addPlugin(pluginId).subscribe({
      next: () => this.loadData()
    });
  }

  removePlugin(pluginId: string): void {
    this.pluginBridge.removePlugin(pluginId).subscribe({
      next: () => this.loadData()
    });
  }

  openPlugin(plugin: Plugin): void {
    window.open(plugin.accessUrl, '_blank');
  }

  openMarketplace(): void {
    this.marketplaceOpen = true;
    this.drawerService.open();
  }

  closeMarketplace(): void {
    this.marketplaceOpen = false;
    this.drawerService.close();
  }
}