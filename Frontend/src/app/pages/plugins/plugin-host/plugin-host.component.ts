import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PluginBridgeService, Plugin } from '../../../core/services/plugin-bridge.service';

@Component({
  selector: 'app-plugin-host',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './plugin-host.component.html',
  styleUrl: './plugin-host.component.scss'
})
export class PluginHostComponent implements OnInit {
  plugin: Plugin | null = null;
  loading = true;
  error = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private pluginBridge: PluginBridgeService
  ) {}

  ngOnInit(): void {
    const pluginId = this.route.snapshot.paramMap.get('pluginId');
    if (!pluginId) {
      this.router.navigate(['/tools']);
      return;
    }

    this.pluginBridge.getAllPlugins().subscribe({
      next: (plugins) => {
        this.plugin = plugins.find(p => p.id === pluginId) || null;
        this.loading = false;
        if (!this.plugin) this.error = true;
      },
      error: () => {
        this.loading = false;
        this.error = true;
      }
    });
  }

  openTool(): void {
    if (this.plugin?.accessUrl) {
      window.open(this.plugin.accessUrl, '_blank');
    }
  }

  goBack(): void {
    this.router.navigate(['/tools']);
  }
}